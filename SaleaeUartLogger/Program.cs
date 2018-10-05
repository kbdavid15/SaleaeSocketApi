using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Saleae.SocketApi;

namespace SaleaeUartLogger
{
    internal class Program
    {
        private static void Main(string[] args)
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
            var active_device = devices.Single(x => x.IsActive);

            Console.WriteLine("currently available devices:");
            devices.ToList().ForEach(x => Console.WriteLine(x.Name));

            Console.WriteLine("currently active device: " + active_device.Name);
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
            if (active_device.DeviceType == DeviceType.Logic8 || active_device.DeviceType == DeviceType.LogicPro8 ||
                active_device.DeviceType == DeviceType.LogicPro16)
            {
                Console.WriteLine("changing active channels");
                client.SetActiveChannels(new[] { 0 });
                Console.WriteLine("");
                //showing active channels
                var active_digital_channels = new List<int>();
                var active_analog_channels = new List<int>();
                client.GetActiveChannels(active_digital_channels, active_analog_channels);
                Console.WriteLine("Active Digital Channels: " + string.Join(", ", active_digital_channels.ToArray()));
                Console.WriteLine("Active Analog Channels: " + string.Join(", ", active_analog_channels.ToArray()));
                //display the currently selected sample rate and performance option.
                var current_sample_rate = client.GetSampleRate();
                Console.WriteLine("The previously selected sample rate was: " + current_sample_rate.DigitalSampleRate +
                                  " SPS (digital), " + current_sample_rate.AnalogSampleRate + " SPS (analog)");
                if (current_sample_rate.AnalogSampleRate > 0 && current_sample_rate.DigitalSampleRate > 0)
                {
                    var current_performance_option = client.GetPerformanceOption();
                    Console.WriteLine("Currently selected performance option: " + current_performance_option);
                }

                //change the sample rate!
                var possible_sample_rates = client.GetAvailableSampleRates();
                if (possible_sample_rates.Any(x => x.DigitalSampleRate == sampleRate))
                {
                    Console.WriteLine($"Changing digital sample rate to {sampleRate}");
                    client.SetSampleRate(possible_sample_rates.First(x => x.DigitalSampleRate == sampleRate));
                    Console.WriteLine("");
                }
                else
                {
                    Console.WriteLine("Selected sample rate not available, exiting");
                    return;
                }

                //set trigger. There are 4 digital channels. all need to be specified.
                Console.WriteLine("setting trigger");
                client.SetTrigger(new[] {Trigger.FallingEdge});
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine(
                    "to see more cool features demoed by this example, please switch to a Logic 8, Logic Pro 8, or Logic Pro 16. Physical or simulation");
            }

            if (active_device.DeviceType == DeviceType.LogicPro8 || active_device.DeviceType == DeviceType.LogicPro16 ||
                active_device.DeviceType == DeviceType.Logic16)
            {
                var voltageOption = client.GetDigitalVoltageOptions().SingleOrDefault(x => x.IsSelected);
                if (voltageOption != null)
                    Console.WriteLine("Currently selected voltage option: " + voltageOption.Description);
            }
            // get the recording time
            Console.WriteLine("");
            Console.WriteLine("Enter the capture time in seconds. Equal to the power cycle time");
            var captureTimeString = Console.ReadLine();
            if (!double.TryParse(captureTimeString, out var captureTimeSec))
            {
                captureTimeSec = 3;
                Console.WriteLine("Invalid entry");
            }


            Console.WriteLine($"Setting capture time to {captureTimeSec}");
            client.SetCaptureSeconds(captureTimeSec);
            Console.WriteLine("");

            var defaultFileName = "Output.csv";
            Console.WriteLine($"Enter sample number or file name identifier or Enter for default: [{defaultFileName}]");
            var fileNameId = Console.ReadLine();
            if (fileNameId == string.Empty) fileNameId = defaultFileName;

            Console.WriteLine("Press Enter to Begin Capture");
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
                    var filePath =
                        $"{Directory.GetCurrentDirectory()}\\{fileNameId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
                    client.ExportAnalyzers(analyzers.First().Index, filePath, false);

                    Console.WriteLine($"Exported data capture #{captureCounter} to {filePath}");
                    captureCounter++;
                    Console.WriteLine("Sleeping for 1 second");
                    Thread.Sleep(1000);
                }       
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

            Console.WriteLine();
            Console.WriteLine("Recording complete, press any key to exit");
            Console.ReadLine();
        }
    }
}