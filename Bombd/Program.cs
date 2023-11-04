using Bombd.Core;
using Bombd.Services;
using Directory = Bombd.Services.Directory;

var server = new BombdServer(new BombdConfiguration
{
    ListenIP = "0.0.0.0",
    ExternalIP = "129.21.215.72",
    ApiURL = "http://127.0.0.1:10050"
});

server.AddService<Directory>();
server.AddService<Matchmaking>();
server.AddService<GameManager>();
server.AddService<GameBrowser>();
server.AddService<PlayGroup>();
server.AddService<TextComm>();
server.AddService<Stats>();

server.Start();