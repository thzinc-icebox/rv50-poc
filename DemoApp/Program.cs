using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using CommandLine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace DemoApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var mre = new ManualResetEvent(false);

            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options))
            {
                Console.Error.WriteLine("Invalid options");
                Environment.Exit(-1);
            }

            var serialNumber = options.SerialNumberOverride ?? "DEFAULT_FAKE_SERIAL_NUMBER";

            var serialPort = new SerialPort(options.SerialPort, 9600, Parity.None, 8, StopBits.One);
            serialPort.Open();

            var client = new MqttClient(options.MqttBroker);

            client.MqttMsgPublished += (sender, e) =>
            {
                Console.WriteLine("Published message ID {0}", e.MessageId);
            };

            client.MqttMsgPublishReceived += (sender, e) =>
            {
                var data = Encoding.UTF8.GetString(e.Message);
                Console.WriteLine("Received message {0}", data);

                if (e.Topic.EndsWith("/serial") && serialPort.IsOpen)
                {
                    serialPort.Write(data);
                }
                else if (e.Topic.EndsWith("/quit"))
                {
                    serialPort.Close();
                    client.Disconnect();
                    mre.Set();
                }
            };

            Console.WriteLine("Connecting to {0}", options.MqttBroker);
            var clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);

            Console.WriteLine("Subscribing to /devices/{0}/#", serialNumber);
            client.Subscribe(new[]
            {
                string.Format("/devices/{0}/#", serialNumber),
            }, new []
            {
                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
            });

            Console.WriteLine("Publishing provisioning message for {0}", serialNumber);
            client.Publish(string.Format("/devices/provision/{0}", serialNumber), Encoding.UTF8.GetBytes(DateTimeOffset.Now.ToString()));
            

            Console.WriteLine("Waiting for messages...");
            mre.WaitOne();
            Console.WriteLine("Finished!");
        }
    }

    public class Options
    {
        [Option('h', "host", DefaultValue = "iot.eclipse.org")]
        public string MqttBroker { get; set; }

        [Option("sn")]
        public string SerialNumberOverride { get; set; }

        [Option("tty", DefaultValue = "/dev/ttyS0")]
        public string SerialPort { get; set; }

    }
}
