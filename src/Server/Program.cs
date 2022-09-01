using Chronos.P2P.Server;

var server = new P2PServer();

server.AddDefaultServerHandler();
Console.WriteLine("Starting signal server at :5000");
await server.StartServerAsync();
