﻿using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Logger
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WindowHeight = 10;
            }

            Console.Title = "Server Log";
            string pipeToken = args[0];

            var pipeClient = new NamedPipeClientStream(".", pipeToken, PipeDirection.InOut, PipeOptions.None);
            if (!pipeClient.IsConnected)
            {
                pipeClient.Connect();
            }

            Console.WriteLine("Wait for messages...\n");

            var reader = new StreamReader(pipeClient);
            while (pipeClient.IsConnected)
            {
                Console.WriteLine(reader.ReadLine());
            }

            Console.WriteLine("Logger has finished working.");
            Console.WriteLine("Press enter to close the window.");
            Console.ReadLine();
        }
    }
}
