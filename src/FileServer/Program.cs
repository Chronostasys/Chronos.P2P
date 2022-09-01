using Chronos.P2P.Client;
using Chronos.P2P.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;


var freePorts = new List<int>();
var port = 3000;
while (true)
{
    var pt = 0;
    if (freePorts.Count > 0)
    {
        pt = freePorts[0];
        freePorts.RemoveAt(0);
    }
    else
    {
        Console.WriteLine("add port:");
        pt = port++;
    }
    var p = new Peer(pt, new(IPAddress.Parse("127.0.0.1"), 5000));
    p.AddHandler<FSHandler>();
    p.OnInitFileTransfer = (f) =>
    {
        return Task.FromResult((true,Path.GetFileName(f.Name)));
    };
    p.PeerConnectionLost += P_PeerConnectionLost;
    _ = p.StartPeer();
    p.OnPeerInvited = (p) =>
    {
        return false;
    };
    while (true)
    {
        var connected = false;
        await Task.Delay(1000);
        foreach (var item in p.Peers)
        {
            var re = await p.SetPeer(item.Key);
            Console.WriteLine($"re {re}");
            if (re)
            {
                Console.WriteLine(item.Value.OuterEP);
                connected = true;
                break;
            }
        }
        if (connected)
        {
            break;
        }
    }
}

void P_PeerConnectionLost(object? sender, EventArgs e)
{
    if (sender is not null && sender is Peer)
    {
        Console.WriteLine("connection lost");
        freePorts.Add((sender as Peer)!.Port);
        ((Peer)sender).Dispose();
        return;
    }
    throw new NotImplementedException();
}

enum Command
{
    LS = 3000,
    LSRESP,
    DOWNLOAD
}


class FSHandler
{
    private readonly Peer peer;
    ILogger<PeerDefaultHandlers> _logger;

    public FSHandler(Peer peer, ILogger<PeerDefaultHandlers> logger)
    {
        this.peer = peer;
        _logger = logger;
    }

    [Handler((int)Command.LS)]
    public async void LSHandler(UdpContext context)
    {
        var path = context.GetData<string>();
        if (!Directory.Exists(path))
        {
            await peer.SendDataToPeerReliableAsync((int)Command.LSRESP, "Path not found");
            context.Dispose();
            return;
        }
        var resp = string.Join("\n", Directory.EnumerateFileSystemEntries(path!).ToList());
        await peer.SendDataToPeerReliableAsync((int)Command.LSRESP, resp);
        context.Dispose();
    }
    [Handler((int)Command.DOWNLOAD)]
    public async void DownloadHandler(UdpContext context)
    {
        var path = context.GetData<string>();
        if (!File.Exists(path))
        {
            await peer.SendDataToPeerReliableAsync((int)Command.LSRESP, "Path not found");
            context.Dispose();
            return;
        }
        await peer.SendFileAsync(path!);
        await peer.SendDataToPeerReliableAsync((int)Command.LSRESP, "Done!");
        context.Dispose();
    }
}