﻿using Chronos.P2P.Client;
using System;
using System.Net;
using System.Threading.Tasks;


var freePorts = new List<int>();
var port = 3000;
while (true)
{
    var pt = 0;
    if (freePorts.Count>0)
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