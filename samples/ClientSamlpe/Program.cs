using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ClientSamlpe
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("worker starting...");
            Console.WriteLine("Enter text (type 'quit' to stop):");

            
            while (true)
            {
                var input = Console.ReadLine();
                Console.WriteLine(input);
                if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Exiting program.");
                    break;
                }
                var content = JsonSerializer.Deserialize<MessageTask>(input);
                Console.WriteLine($"Message : {content.Message} group : {content.Group}");

                //delay mock logical
                Thread.Sleep(500);
            }

        }
    }

    public class MessageTask
    {
        public string Group { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; }
    }
}

