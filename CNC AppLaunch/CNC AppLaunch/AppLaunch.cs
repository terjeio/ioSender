/*
 * AppLaunch.cs - part of CNC Library
 *
 * v0.33 / 2021-05-17 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2020-2021, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace CNC.AppLaunch
{
    class AppLaunch
    {
        static void Main(string[] args)
        {
            string filename;
            int ret = 0;

            if (args.Length == 1 && File.Exists(args[0]))
            {
                filename = args[0];

                using (var pipeClient = new NamedPipeClientStream(".", "ioSender", PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation))
                {
                    try
                    {
                        pipeClient.Connect(200);
                        using (var pipe = new StreamWriter(pipeClient))
                        {
                            pipe.WriteLine(filename);
                        }
                        pipeClient.Close();
                    }
                    catch (Exception ex)
                    {
                        if (ex is System.TimeoutException)
                        {
                            string cmd = AppDomain.CurrentDomain.BaseDirectory + "ioSender.exe";
                            if (File.Exists(cmd))
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo()
                                {
                                    FileName = cmd,
                                    Arguments = '"' + filename + '"'
                                };
                                Process.Start(startInfo);
                            }
                        }
                        else
                            ret = 2;
                    }
                    finally
                    {
                        Environment.Exit(ret);
                    }
                }
            }
            else
                Environment.Exit(1);
        }
    }
}
