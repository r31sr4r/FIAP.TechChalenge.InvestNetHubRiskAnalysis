using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

class Program
{
    public static IConfigurationRoot? Configuration { get; set; }
    static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        Configuration = builder.Build();

        var rabbitConfig = Configuration.GetSection("RabbitMQ");
        var factory = new ConnectionFactory()
        {
            HostName = rabbitConfig["HostName"],
            Port = Convert.ToInt32(rabbitConfig["Port"]),
            UserName = rabbitConfig["UserName"],
            Password = rabbitConfig["Password"]
        };

        Console.WriteLine("Iniciando o Consumidor de Análise de Risco...");

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: rabbitConfig["UserCreatedQueue"],
                             durable: true,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Recebida mensagem: {message}");

            try
            {
                // Simular análise de risco e esperar pela conclusão
                //await SimularAnaliseDeRisco(message);
                await SimularAnaliseDeRisco(channel, rabbitConfig, message);
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mensagem: {ex.Message}");
                // Enviar nack em caso de falha para reenfileirar a mensagem
                channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        channel.BasicConsume(queue: rabbitConfig["UserCreatedQueue"],
                             autoAck: false,
                             consumer: consumer);

        Console.WriteLine("Pressione [ENTER] para sair.");
        Console.ReadLine();
    }


    static async Task SimularAnaliseDeRisco(IModel channel, IConfigurationSection rabbitConfig, string mensagem)
    {
        var dadosUsuario = JsonConvert.DeserializeObject<dynamic>(mensagem);
        var userId = dadosUsuario?.user_id.ToString();
        var cpf = dadosUsuario?.cpf.ToString();

        // Definindo a cor do texto para Cyan para informações iniciais
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Iniciando análise de risco para o cliente de Id {userId}, CPF {cpf}");
        Console.ResetColor(); // Reseta para a cor padrão

        // Etapas da simulação com delays
        Console.ForegroundColor = ConsoleColor.Yellow; // Amarelo para avisos ou etapas intermediárias
        Console.WriteLine("Realizando validações de perfil de investimento...");
        await Task.Delay(500);
        Console.WriteLine("Realizando validações de perfil de risco de crédito...");
        await Task.Delay(500);
        Console.WriteLine("Realizando validações de setores de interesse...");
        await Task.Delay(500);
        Console.ResetColor();

        // Simulando diferentes cenários
        var random = new Random();
        int scenario = random.Next(1, 5);

        string response;

        if (scenario <= 3) // Cenários de sucesso
        {
            Console.ForegroundColor = ConsoleColor.Green; // Verde para sucesso
            var riskLevel = scenario == 1 ? "Low" : scenario == 2 ? "Medium" : "High";
            response = GenerateSuccessResponse(userId, riskLevel);
            Console.WriteLine($"Simulação concluída para o cliente de Id {userId}. Resposta: {response}");
        }
        else // Cenário de erro
        {
            Console.ForegroundColor = ConsoleColor.Red; // Vermelho para erro
            response = GenerateErrorResponse(userId);
            Console.WriteLine($"Erro na simulação para o cliente de Id {userId}. Resposta: {response}");
        }
        Console.ResetColor(); // Reseta para a cor padrão após a operação

        // Simulando o processamento da resposta
        await Task.Delay(1000); // Simulação do tempo de processamento

        // Obter as configurações do RabbitMQ do appsettings.json
        var exchange = rabbitConfig["Exchange"];
        var routingKey = rabbitConfig["UserAnalysisResultRountingKey"];

        // Publicar a mensagem de resposta
        var responseBytes = Encoding.UTF8.GetBytes(response);
        channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: null,
            body: responseBytes
        );

        Console.WriteLine($"Resultado da análise publicado na exchange '{exchange}' com a routing key '{routingKey}'.");
    }

    static string GenerateSuccessResponse(string userId, string riskLevel)
    {
        // Gera a resposta de sucesso
        return JsonConvert.SerializeObject(new
        {
            id = Guid.NewGuid().ToString(),
            status = "COMPLETED",
            user = new
            {
                resource_id = userId,
                risk_level = riskLevel,
                investment_preferences = JsonConvert.SerializeObject(new
                {
                    assetTypes = new[] { "Stocks", "Bonds" },
                    investmentHorizon = "LongTerm",
                    interestedSectors = new[] { "Technology", "Healthcare" }
                })
            },
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow.AddMinutes(5)
        });
    }

    static string GenerateErrorResponse(string userId)
    {
        // Gera a resposta de erro
        return JsonConvert.SerializeObject(new
        {
            message = new
            {
                resource_id = userId,
                risk_level = "",
                investment_preferences = ""
            },
            error = "Descrição do erro"
        });
    }

}
