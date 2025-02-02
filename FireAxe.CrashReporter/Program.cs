﻿using System;
using System.IO.Pipes;

namespace FireAxe.CrashReporter
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("====FireAxe CrashReporter====\n");

            if (args.Length == 0)
            {
                Console.WriteLine("Error: no args");
                Console.ReadLine();
                return;
            }

            var pipeName = args[0];
            try
            {
                using (var pipeClient = new NamedPipeClientStream(pipeName))
                {
                    pipeClient.Connect();
                    using (var reader = new BinaryReader(pipeClient))
                    {
                        Console.WriteLine(reader.ReadString());
                    }
                }
            }
            catch (IOException ex) 
            {
                Console.WriteLine(ex);
            }

            Console.ReadLine();
        }
    }
}