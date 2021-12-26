/*
 * PipeServer.cs - part of Grbl Code Sender
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

// https://www.c-sharpcorner.com/article/aborting-thread-vs-cancelling-task/

using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace CNC.Controls
{
    public class PipeServer
    {
        public delegate void FileTransferHandler(string filename);
        public static event FileTransferHandler FileTransfer;

        public PipeServer(System.Windows.Threading.Dispatcher dispatcher)
        {
            Task server = null;

            if(!File.Exists("ioSender"))
                server = Task.Factory.StartNew(() => RunServer(dispatcher));
        }

        private static void RunServer(System.Windows.Threading.Dispatcher dispatcher)
        {
            string filename; int c;

            try {

                using (var pipeServer = new NamedPipeServerStream("ioSender", PipeDirection.InOut))
                {
                    using (var reader = new StreamReader(pipeServer))
                    {
                        using (var writer = new StreamWriter(pipeServer))
                        {
                            while (true)
                            {
                                filename = string.Empty;

                                pipeServer.WaitForConnection();

                                //writer.WriteLine("Hello");
                                //writer.Flush();
                                //pipeServer.WaitForPipeDrain();

                                while (pipeServer.IsConnected)
                                {
                                    if ((c = reader.Read()) != -1)
                                    {
                                        if (c >= ' ')
                                            filename += (char)c;
                                        else if (c == 10 && FileTransfer != null && File.Exists(filename))
                                            dispatcher.Invoke(FileTransfer, filename);
                                    }
                                }
                                pipeServer.Disconnect();
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        } 
    }
}
