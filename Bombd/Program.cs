using Bombd.Core;
using Bombd.Services;
using Directory = Bombd.Services.Directory;

var server = new BombdServer();

server.AddService<Directory>();
server.AddService<Matchmaking>();
server.AddService<GameManager>();
server.AddService<GameBrowser>();
server.AddService<PlayGroup>();
server.AddService<TextComm>();
server.AddService<Stats>();

server.Start();