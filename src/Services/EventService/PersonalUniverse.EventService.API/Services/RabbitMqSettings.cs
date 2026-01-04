namespace PersonalUniverse.EventService.API.Services;

public class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "personaluniverse.events";
    public string ExchangeType { get; set; } = "topic";
    public bool UseSsl { get; set; } = false;
}
