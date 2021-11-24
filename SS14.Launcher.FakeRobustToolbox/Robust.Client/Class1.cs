using System;
using Robust.LoaderApi;

[assembly: LoaderEntryPoint(typeof(Robust.Client.FakeClient))]

﻿namespace Robust.Client;

public class FakeClient : ILoaderEntryPoint
{
    public void Main(IMainArgs args)
    {
        var redial = args.RedialApi;
        if (redial == null)
        {
            Console.WriteLine("Cannot redial");
        }
        else
        {
            Console.WriteLine("Redialling");
            redial.Redial(new Uri("ss14://localhost:1212"), "Example text\nVery long example text with multiple lines.");
        }
    }
}

