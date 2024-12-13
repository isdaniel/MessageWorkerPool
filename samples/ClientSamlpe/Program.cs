Console.WriteLine("worker starting...");
Console.WriteLine("Enter text (type 'quit' to stop):");

while (true)
{
    var input = Console.ReadLine();

    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Exiting program.");
        break;
    }

    Console.WriteLine($"process get string from message pool: {input}");
}