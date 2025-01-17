// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public static class TestHelpers
{
    public static async Task<(TResult, IModel, IConnection)> WaitForMessageResult<TResult>(
        string replyQueue, Func<string, TResult> action)
    {
        IConnection connection;
        IModel channel;

        var rabbitMqSetting = new RabbitMqSetting
        {
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : (ushort)5672,
        };

        var factory = new ConnectionFactory { Uri = rabbitMqSetting.GetUri() };
        TaskCompletionSource<TResult> messageReceived = new TaskCompletionSource<TResult>();
        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        channel.QueueDeclare(replyQueue, true, false, false, null);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (sender, e) =>
        {
            try
            {
                var message = Encoding.UTF8.GetString(e.Body.Span);
                messageReceived.SetResult(action(message));
                channel.BasicAck(e.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                messageReceived.SetException(ex);
            }
        };

        channel.BasicQos(0, 1, false);
        channel.BasicConsume(replyQueue, false, consumer);

        TResult res = await messageReceived.Task.ConfigureAwait(false);

        return (res, channel, connection);
    }

    public static void SendingMessage(string bodyMessage, string correlationId, string queueName, string replyQueue, Dictionary<string, object> header = null)
    {
        using (MessageClient messageClient = new MessageClient(new MessageClientOptions()
        {
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
            Password = Environment.GetEnvironmentVariable("PASSWORD") ?? "guest",
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "127.0.0.1",
            Port = ushort.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out ushort port) ? port : (ushort)5672,
            ExchangeName = Environment.GetEnvironmentVariable("EXCHANGENAME") ?? "integration-exchange"
        }))
        {
            messageClient.PublishMessage(queueName, bodyMessage, correlationId, header, replyQueue);
        }
    }
}
