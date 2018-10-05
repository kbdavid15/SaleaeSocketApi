using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Saleae.SocketApi;

namespace SaleaeUartLogger
{
    internal class Program
    {
        private static void Main()
        {
            RunProgram();
        }

        private static void RunProgram()
        {
            SaleaeClient client;

            var sampleRate = 10000000;

            //Set this variable to have all text socket commands printed to the console.
            SaleaeClient.PrintCommandsToConsole = false;

            //Make sure to enable the socket server in the Logic software preferences, and make sure that it is running!

            //This demo is designed to show some common socket commands, and interacts best with either the simulation or real Logic 8, Logic Pro 8, or Logic Pro 16.
            var host = "127.0.0.1";
            var port = 10429;

            //attempt to connect with socket.
            Console.WriteLine("Connecting...");
            try
            {
                client = new SaleaeClient(host, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while connecting: " + ex.Message);
                Console.ReadLine();
                return;
            }

            StringHelper.WriteLine("Connected");
            Console.WriteLine("");

            //Get a list of connected devices. (when no devices are connected, this should return 4 simulation devices)
            var devices = client.GetConnectedDevices();
            var activeDevice = devices.Single(x => x.IsActive);

            Console.WriteLine("currently available devices:");
            devices.ToList().ForEach(x => Console.WriteLine(x.Name));

            Console.WriteLine("currently active device: " + activeDevice.Name);
            Console.WriteLine("");

            //list all analyzers currently added to the session.
            var analyzers = client.GetAnalyzers();

            if (analyzers.Any())
            {
                Console.WriteLine("Current analyzers:");
                analyzers.ToList().ForEach(x => Console.WriteLine(x.AnalyzerType));
                Console.WriteLine("");
            }

            //change the active channels, but only if the active device supports that.
            if (activeDevice.DeviceType != DeviceType.Logic8 && activeDevice.DeviceType != DeviceType.LogicPro8 &&
                activeDevice.DeviceType != DeviceType.LogicPro16)
            {
                Console.WriteLine("No supported device connected. Press any key to exit");
                Console.ReadLine();
                return;
            }

            Console.Write("Enter number of channels to log: ");
            var numChannels = Console.ReadLine();

            if (int.TryParse(numChannels, out var iNumChannels) && iNumChannels <= 8)
            {
                var channelsToActivate = new int[iNumChannels];
                for (var i = 0; i < channelsToActivate.Length; i++)
                {
                    channelsToActivate[i] = i;
                }

                Console.WriteLine($"Activating channels {string.Join(" ", channelsToActivate)}");
                client.SetActiveChannels(channelsToActivate);
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("Invalid entry, only using channel 0");
                client.SetActiveChannels(new[] {0});
                Console.WriteLine("");
            }


            //showing active channels
            var activeDigitalChannels = new List<int>();
            var activeAnalogChannels = new List<int>();
            client.GetActiveChannels(activeDigitalChannels, activeAnalogChannels);
            Console.WriteLine("Active Digital Channels: " + string.Join(", ", activeDigitalChannels.ToArray()));
            Console.WriteLine("Active Analog Channels: " + string.Join(", ", activeAnalogChannels.ToArray()));
            //display the currently selected sample rate and performance option.
            var currentSampleRate = client.GetSampleRate();
            Console.WriteLine("The previously selected sample rate was: " + currentSampleRate.DigitalSampleRate +
                              " SPS (digital), " + currentSampleRate.AnalogSampleRate + " SPS (analog)");
            if (currentSampleRate.AnalogSampleRate > 0 && currentSampleRate.DigitalSampleRate > 0)
            {
                var currentPerformanceOption = client.GetPerformanceOption();
                Console.WriteLine("Currently selected performance option: " + currentPerformanceOption);
            }

            //change the sample rate!
            var possibleSampleRates = client.GetAvailableSampleRates();
            if (possibleSampleRates.Any(x => x.DigitalSampleRate == sampleRate))
            {
                Console.WriteLine($"Changing digital sample rate to {sampleRate}");
                client.SetSampleRate(possibleSampleRates.First(x => x.DigitalSampleRate == sampleRate));
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("Selected sample rate not available, exiting");
                return;
            }

            //set trigger. There are 4 digital channels. all need to be specified.
            Console.WriteLine("Setting trigger to CH0 falling edge");

            var triggers = new Trigger[activeDigitalChannels.Count];

            for (var i = 0; i < triggers.Length; i++)
            {
                triggers[i] = Trigger.None;
            }

            triggers[0] = Trigger.FallingEdge;

            client.SetTrigger(triggers);
            Console.WriteLine("");

            if (activeDevice.DeviceType == DeviceType.LogicPro8 || activeDevice.DeviceType == DeviceType.LogicPro16 ||
                activeDevice.DeviceType == DeviceType.Logic16)
            {
                var voltageOption = client.GetDigitalVoltageOptions().SingleOrDefault(x => x.IsSelected);
                if (voltageOption != null)
                    Console.WriteLine("Currently selected voltage option: " + voltageOption.Description);
            }
            // get the recording time
            Console.WriteLine("");
            Console.Write("Enter the capture time in seconds. Equal to the power cycle time: ");
            var captureTimeString = Console.ReadLine();
            if (!double.TryParse(captureTimeString, out var captureTimeSec))
            {
                captureTimeSec = 3;
                Console.WriteLine("Invalid entry");
            }


            Console.WriteLine($"Setting capture time to {captureTimeSec}");
            client.SetCaptureSeconds(captureTimeSec);
            Console.WriteLine("");


            var fileNameIds = new string[activeDigitalChannels.Count];
            var fileDirectories = new string[activeDigitalChannels.Count];
            for (var i = 0; i < activeDigitalChannels.Count; i++)
            {
                var activeDigitalChannel = activeDigitalChannels[i];
                var defaultFileName = $"CH{activeDigitalChannel}_output";
                Console.Write(
                    $"Enter file name identifier for channel {activeDigitalChannel} or Enter for default [{defaultFileName}]: ");
                var fileNameId = Console.ReadLine();
                if (fileNameId == string.Empty) fileNameId = defaultFileName;

                fileNameIds[i] = fileNameId;

                var directoryName = $"{Directory.GetCurrentDirectory()}\\{fileNameId}_{DateTime.Now:yyyy-MM-dd}";

                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                fileDirectories[i] = directoryName;
            }


            Console.Write("Press Enter to Begin Capture");
            Console.ReadLine();
            Console.WriteLine("Press ESC to stop");

            var captureCounter = 1;

            do 
            {
                while (! Console.KeyAvailable) 
                {
                    Console.WriteLine($"Starting capture #{captureCounter}");
                    client.Capture();

                    // once capture is complete, save the data to file
                    for (var i = 0; i < analyzers.Count; i++)
                    {
                        if (fileNameIds.Length <= i) continue;
                        var filePath =
                            $"{fileDirectories[i]}\\{fileNameIds[i]}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
                        client.ExportAnalyzers(analyzers[i].Index, filePath, false);

                        Console.WriteLine($"Exported {analyzers[i].AnalyzerType} analyzer #{analyzers[i].Index} data capture #{captureCounter} to {filePath}");
                    }


                    captureCounter++;
                }       
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

            Console.WriteLine();
            Console.WriteLine("Recording complete, press any key to exit");
            Console.ReadLine();
        }
    }
}