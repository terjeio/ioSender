/*
 * Comms.cs - part of CNC Controls library
 *
 * v0.02 / 2019-09-21 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2019, Io Engineering (Terje Io)
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

//#define USEELTIMA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;

namespace CNC.Core
{
    public delegate void DataReceivedHandler(string data);

    #region Interface
    public class Comms
    {
        public enum State
        {
            AwaitAck,
            DataReceived,
            ACK,
            NAK
        }

        public enum ResetMode
        {
            None,
            DTR,
            RTS
        }

        public const int TXBUFFERSIZE = 4096, RXBUFFERSIZE = 1024;

        public static StreamComms com = null;
    }

    public interface StreamComms
    {

        bool IsOpen { get; }
        int OutCount { get; }
        string Reply { get; }
        Comms.State CommandState { get; set; }

        void Close();
        void WriteByte(byte data);
        void WriteBytes(byte[] bytes, int len);
        void WriteString(string data);
        void WriteCommand(string command);
        string getReply(string command);
        void AwaitAck();
        void AwaitAck(string command);
        void AwaitResponse(string command);
        void AwaitResponse();
        void PurgeQueue();

        event DataReceivedHandler DataReceived;
    }
    #endregion

#if USEELTIMA
    #region ELTIMA_SERIAL
public class SerialComms : StreamComms
{

    private SPortLib.SPortAx SerialPort;
    private StringBuilder input = new StringBuilder(300);
    private volatile Comms.State state = Comms.State.ACK;

    public event DataReceivedHandler DataReceived;

    public SerialComms (string PortParams, Comms.ResetMode ResetMode)
    {
        Comms.com = this;
        this.Reply = "";

        try
        {
            serialPort = new SPortLib.SPortAx();
        }
        catch
        {
            MessageBox.Show("Failed to load serial port driver.", "GCode Sender");
            System.Environment.Exit(1);
        }

        serialPort.InitString(PortParams.Substring(PortParams.IndexOf(":") + 1));
//           serialPort.HandShake = 0x08; // Cannot be used with ESP32
        serialPort.FlowReplace = 0x80;
        serialPort.CharEvent = 10;
        serialPort.InBufferSize = Comms.RXBUFFERSIZE;
        serialPort.OutBufferSize = Comms.TXBUFFERSIZE;
        serialPort.BlockMode = false;


        serialPort.Open(PortParams.Substring(0, PortParams.IndexOf(":")));
        serialPort.OnRxFlag += new SPortLib._ISPortAxEvents_OnRxFlagEventHandler(this.SerialRead);

        System.Threading.Thread.Sleep(500);

        if (serialPort.IsOpened)
        {
            /* For resetting ESP32, use DTR for Arduino
            serialPort.RtsEnable = true;
            serialPort.RtsEnable = false;
            System.Threading.Thread.Sleep(300);
            */
            serialPort.PurgeQueue();
            serialPort.OnRxFlag += new SPortLib._ISPortAxEvents_OnRxFlagEventHandler(this.SerialRead);
        }
    }

    ~SerialComms()
    {
        this.Close();
    }

    public Comms.State CommandState { get { return this.state; } set { this.state = value; } }
    public string Reply { get; private set; }
    public bool IsOpen { get { return serialPort.IsOpened; } }
    public int OutCount { get { return serialPort.OutCount; } }

    public void PurgeQueue()
    {
        serialPort.PurgeQueue();
    }

    public void Close()
    {
        if(this.IsOpen)
            serialPort.Close();                
    }

    public void WriteByte(byte data)
    {
        serialPort.Write(ref data, 1);
    }

    public void WriteBytes(byte[] bytes, int len)
    {
        serialPort.Write(ref bytes[0], len);
    }

    public void WriteString(string data)
    {
        serialPort.WriteStr(data);
    }

    public void WriteCommand (string command)
    {
        this.state = Comms.State.AwaitAck;

        if (command.Length == 1 && command != GrblConstants.CMD_PROGRAM_DEMARCATION)
            WriteByte((byte)command.ToCharArray()[0]);
        else if (command.Length > 0)
        {
            command += "\r";
            serialPort.WriteStr(command);
        }
    }

    public void AwaitAck()
    {
        while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
            EventUtils.DoEvents();
    }

    public string getReply(string command)
    {
        this.Reply = "";
        this.WriteCommand(command);

        while (state == Comms.State.AwaitAck)
            EventUtils.DoEvents();

        return Reply;
    }

    private void SerialRead()
    {
        int pos = 0;

        lock (input)
        {
            input.Append(serialPort.ReadStr());

            while (input.Length > 0 && (pos = input.ToString().IndexOf('\n')) > 0)
            {
                Reply = input.ToString(0, pos - 1);
                input.Remove(0, pos + 1);
                state = Reply == "ok" ? Comms.State.ACK : (Reply.StartsWith("error") ? Comms.State.NAK : Comms.State.DataReceived);
                if (Reply.Length != 0 && DataReceived != null)
                    DataReceived(Reply);
            }
        }
    }
}
    #endregion
#else
    #region MS_SERIAL
    public class SerialComms : StreamComms
    {

        private SerialPort serialPort = null;
        private StringBuilder input = new StringBuilder(400);
        private volatile Comms.State state = Comms.State.ACK;
        private System.Windows.Threading.Dispatcher Dispatcher { get; set; }

        public event DataReceivedHandler DataReceived;

#if RESPONSELOG
    StreamWriter log = null;
#endif

        public SerialComms(string PortParams, Comms.ResetMode ResetMode, System.Windows.Threading.Dispatcher dispatcher)
        {
            Comms.com = this;
            Dispatcher = dispatcher;
            Reply = "";

            string[] parameter = PortParams.Substring(PortParams.IndexOf(":") + 1).Split(',');

            if (parameter.Count() < 4)
            {
                MessageBox.Show("Unable to open serial port: " + PortParams, "GCode Sender");
                System.Environment.Exit(2);
            }

            serialPort = new SerialPort();
            serialPort.PortName = PortParams.Substring(0, PortParams.IndexOf(":"));
            serialPort.BaudRate = int.Parse(parameter[0]);
            serialPort.Parity = ParseParity(parameter[1]);
            serialPort.DataBits = int.Parse(parameter[2]);
            serialPort.StopBits = int.Parse(parameter[3]) == 1 ? StopBits.One : StopBits.Two;
            serialPort.ReceivedBytesThreshold = 1;
            serialPort.ReadTimeout = 5000;
            serialPort.NewLine = "\r\n";
            serialPort.ReadBufferSize = Comms.RXBUFFERSIZE;
            serialPort.WriteBufferSize = Comms.TXBUFFERSIZE;

            if (parameter.Count() > 4) switch (parameter[4])
                {
                    case "P": // Cannot be used With ESP32!
                        serialPort.Handshake = Handshake.RequestToSend;
                        break;

                    case "X":
                        serialPort.Handshake = Handshake.XOnXOff;
                        break;
                }

            try
            {
                serialPort.Open();
            }
            catch
            {
            }

            if (serialPort.IsOpen)
            {
                PurgeQueue();
                serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);

                switch (ResetMode)
                {
                    case Comms.ResetMode.RTS:
                        /* For resetting ESP32 */
                        serialPort.RtsEnable = true;
                        serialPort.RtsEnable = false;
                        System.Threading.Thread.Sleep(2000);
                        break;

                    case Comms.ResetMode.DTR:
                        /* For resetting Arduino */
                        serialPort.DtrEnable = true;
                        serialPort.DtrEnable = false;
                        System.Threading.Thread.Sleep(2000);
                        break;
                }

#if RESPONSELOG
            log = new StreamWriter(@"D:\grbl.txt");
#endif
            }
        }

        ~SerialComms()
        {
#if RESPONSELOG
        if(log != null) try
        {
            log.Close();
            log = null;
        }
        catch { }
#endif
            if(!IsClosing && IsOpen)
                Close();
        }

        public Comms.State CommandState { get { return state; } set { state = value; } }
        public string Reply { get; private set; }
        public bool IsOpen { get { return serialPort != null && serialPort.IsOpen; } }
        public bool IsClosing { get; private set; }
        public int OutCount { get { return serialPort.BytesToWrite; } }

        public void PurgeQueue()
        {
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            Reply = "";
        }

        private Parity ParseParity(string parity)
        {
            Parity res = Parity.None;

            switch (parity)
            {
                case "E":
                    res = Parity.Even;
                    break;

                case "O":
                    res = Parity.Odd;
                    break;

                case "M":
                    res = Parity.Mark;
                    break;

                case "S":
                    res = Parity.Space;
                    break;
            }

            return res;
        }

        public void Close()
        {
            if (!IsClosing && IsOpen)
            {
                IsClosing = true;
                try
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.DtrEnable = false;
                    serialPort.RtsEnable = false;
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                    System.Threading.Thread.Sleep(100);
                    serialPort.Close();
                    serialPort = null;
                }
                catch { }
                IsClosing = false;
            }
        }

        public void WriteByte(byte data)
        {
            serialPort.BaseStream.Write(new byte[1] { data }, 0, 1);
            //serialPort.Write(new byte[1] { data }, 0, 1);
        }

        public void WriteBytes(byte[] bytes, int len)
        {
            serialPort.BaseStream.Write(bytes, 0, len);
        //      serialPort.Write(bytes, 0, len);
        }

        public void WriteString(string data)
        {
            serialPort.Write(data);
        }

        public void WriteCommand(string command)
        {
            state = Comms.State.AwaitAck;

            if (command.Length == 1 && command != GrblConstants.CMD_PROGRAM_DEMARCATION)
                WriteByte((byte)command.ToCharArray()[0]);
            else if (command.Length > 0)
            {
                command += "\r";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(command);
                serialPort.BaseStream.Write(bytes, 0, bytes.Length);
                // serialPort.Write(command);
            }
        }

        public void AwaitAck()
        {
            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }
        public void AwaitAck(string command)
        {
            PurgeQueue();
            Reply = "";
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
                System.Threading.Thread.Sleep(10);
        }

        public void AwaitResponse()
        {
            while (Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }

        public void AwaitResponse(string command)
        {
            PurgeQueue();
            Reply = "";
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.AwaitAck)
                System.Threading.Thread.Sleep(15);
        }

        public string getReply(string command)
        {
            Reply = "";
            WriteCommand(command);

            AwaitResponse();

            return Reply;
        }

        void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int pos = 0;

            lock (input)
            {
                input.Append(serialPort.ReadExisting());

                while (input.Length > 0 && (pos = input.ToString().IndexOf('\n')) > 0)
                {
                    Reply = input.ToString(0, pos - 1);
                    input.Remove(0, pos + 1);
#if RESPONSELOG
                log.WriteLine(Reply);
#endif
                    if (Reply.Length != 0 && DataReceived != null)
                        Dispatcher.Invoke(DataReceived, Reply);

                    state = Reply == "ok" ? Comms.State.ACK : (Reply.StartsWith("error") ? Comms.State.NAK : Comms.State.DataReceived);
                }
            }
        }
    }
    #endregion
#endif
    #region IP_COMMS
    public class IPComms : StreamComms
    {
        private TcpClient ipserver = null;
        private NetworkStream ipstream = null;
        private byte[] buffer = new byte[512];
        private volatile Comms.State state = Comms.State.ACK;
        private StringBuilder input = new StringBuilder(300);

        public event DataReceivedHandler DataReceived;

        public IPComms(string host)
        {
            Comms.com = this;
            Reply = "";

            string[] parameter = host.Split(':');

            if (parameter.Length == 2) try
                {
                    ipserver = new TcpClient(parameter[0], int.Parse(parameter[1]));
                    ipstream = ipserver.GetStream();
                    ipstream.BeginRead(buffer, 0, buffer.Length, ReadComplete, buffer);
                }
                catch
                {
                }
        }

        ~IPComms()
        {
            Close();
        }

        public bool IsOpen { get { return ipserver != null && ipserver.Connected; } }
        public int OutCount { get { return 0; } }
        public Comms.State CommandState { get { return state; } set { state = value; } }
        public string Reply { get; private set; }

        public void PurgeQueue()
        {
            while (ipstream.DataAvailable)
                ipstream.ReadByte();
            Reply = "";
        }

        public void Close()
        {
            if (IsOpen)
            {
                ipstream.Close(300);
                ipstream = null;
                ipserver.Close();
            }
        }

        public void WriteByte(byte data)
        {
            ipstream.Write(new byte[1] { data }, 0, 1);
        }

        public void WriteBytes(byte[] bytes, int len)
        {
            ipstream.Write(bytes, 0, len);
        }

        public void WriteString(string data)
        {
            byte[] bytes = Encoding.Default.GetBytes(data);
            ipstream.Write(bytes, 0, bytes.Length);
        }

        public void WriteCommand(string command)
        {
            state = Comms.State.AwaitAck;

            if (command.Length > 1 || command == GrblConstants.CMD_PROGRAM_DEMARCATION)
                command += "\r";

            WriteString(command);
        }

        public void AwaitAck()
        {
            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }

        public void AwaitAck(string command)
        {
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck);
        }

        public void AwaitResponse()
        {
            while (Comms.com.CommandState == Comms.State.AwaitAck)
                EventUtils.DoEvents();
        }
        public void AwaitResponse(string command)
        {
            WriteCommand(command);

            while (Comms.com.CommandState == Comms.State.AwaitAck);
        }

        public string getReply(string command)
        {
            Reply = "";
            WriteCommand(command);

            while (state == Comms.State.AwaitAck)
                EventUtils.DoEvents();

            return Reply;
        }

        void ReadComplete(IAsyncResult iar)
        {
            int bytesAvailable = 0;
            byte[] buffer = (byte[])iar.AsyncState;

            try
            {
                bytesAvailable = ipstream.EndRead(iar);
            }
            catch
            {
                // error handling required here (and many other places)...
            }

            int pos = 0;

            lock (input)
            {
                input.Append(Encoding.ASCII.GetString(buffer, 0, bytesAvailable));

                while (input.Length > 0 && (pos = input.ToString().IndexOf('\n')) > 0)
                {
                    Reply = input.ToString(0, pos - 1);
                    input.Remove(0, pos + 1);
                    state = Reply == "ok" ? Comms.State.ACK : (Reply.StartsWith("error") ? Comms.State.NAK : Comms.State.DataReceived);
                    if (Reply.Length != 0 && DataReceived != null)
                        DataReceived(Reply);
                }
            }

            if (ipstream != null)
                ipstream.BeginRead(buffer, 0, buffer.Length, ReadComplete, buffer);
        }
    }
    #endregion

    public static class EventUtils
    {
        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        public static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }
    }
}

