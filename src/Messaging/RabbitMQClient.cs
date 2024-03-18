using RabbitMQ.Client;

namespace Messaging;
public class RabbitMQClient
{
    private readonly IConnection connection;
    private readonly IModel channel;

    public RabbitMQClient(string uri)
    {
        var factory = new ConnectionFactory() { Uri = new Uri(uri) };
        connection = factory.CreateConnection();
        channel = connection.CreateModel();

        // Declara a fila user.created se ela não existir
        channel.QueueDeclare(queue: "user.created",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        // Configurar a exchange e o binding conforme necessário
    }

    public IModel GetChannel() => channel;

    public void Close()
    {
        channel?.Close();
        connection?.Close();
    }
}
