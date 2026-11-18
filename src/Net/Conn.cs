using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Vesperfell
{
    /// <summary>Little-endian message writer with a growable buffer.</summary>
    public sealed class MsgW
    {
        public byte[] B = new byte[512];
        public int N;

        void Need(int k) { if (N + k > B.Length) Array.Resize(ref B, Math.Max(B.Length * 2, N + k + 64)); }
        public MsgW Begin(int type) { N = 0; return Byte(type); }
        public MsgW Byte(int v) { Need(1); B[N++] = (byte)v; return this; }
        public MsgW Bool(bool v) { return Byte(v ? 1 : 0); }
        public MsgW Short(int v) { Need(2); B[N++] = (byte)v; B[N++] = (byte)(v >> 8); return this; }
        public MsgW Int(int v) { Need(4); B[N++] = (byte)v; B[N++] = (byte)(v >> 8); B[N++] = (byte)(v >> 16); B[N++] = (byte)(v >> 24); return this; }
        public unsafe MsgW Float(float v) { return Int(*(int*)&v); }
        public MsgW Str(string s)
        {
            byte[] u = Encoding.UTF8.GetBytes(s ?? "");
            int len = Math.Min(u.Length, 60000);
            Short(len); Need(len);
            Buffer.BlockCopy(u, 0, B, N, len); N += len;
            return this;
        }
    }

    /// <summary>Message reader. Reads past the end return zeros instead of throwing.</summary>
    public sealed class MsgR
    {
        readonly byte[] b; int p;
        public MsgR(byte[] data) { b = data; }
        public bool End { get { return p >= b.Length; } }
        public int Byte() { return p < b.Length ? b[p++] : 0; }
        public bool Bool() { return Byte() != 0; }
        public int Short() { int v = (short)(Byte() | (Byte() << 8)); return v; }
        public int UShort() { return Byte() | (Byte() << 8); }
        public int Int() { return Byte() | (Byte() << 8) | (Byte() << 16) | (Byte() << 24); }
        public unsafe float Float() { int v = Int(); float f = *(float*)&v; return float.IsNaN(f) || float.IsInfinity(f) ? 0 : f; }
        public string Str()
        {
            int len = UShort();
            if (len <= 0 || p + len > b.Length) { p = Math.Min(b.Length, p + Math.Max(0, len)); return ""; }
            string s = Encoding.UTF8.GetString(b, p, len); p += len;
            return s;
        }
    }

    /// <summary>
    /// One TCP connection carrying length-prefixed messages. Two background threads do the blocking work:
    /// one reads whole messages into an inbox, one writes whatever has been queued. The game thread never waits.
    /// </summary>
    public sealed class Conn
    {
        const int MaxMessage = 1 << 20, MaxPending = 8 << 20;
        readonly Socket sock; readonly Thread reader, writer;
        readonly Queue<byte[]> inbox = new Queue<byte[]>();
        readonly object gate = new object(), sendGate = new object(), sockGate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        byte[] pend = new byte[8192]; int pendN;
        public volatile bool Closed;
        public string Remote = "";
        public long BytesIn, BytesOut;

        public Conn(Socket s)
        {
            sock = s;
            try { s.NoDelay = true; s.SendTimeout = 5000; s.SendBufferSize = 1 << 17; Remote = s.RemoteEndPoint.ToString(); } catch { }
            reader = new Thread(ReadLoop); reader.IsBackground = true; reader.Name = "Vesperfell net in"; reader.Start();
            writer = new Thread(WriteLoop); writer.IsBackground = true; writer.Name = "Vesperfell net out"; writer.Start();
        }

        void ReadLoop()
        {
            try
            {
                byte[] hdr = new byte[4];
                while (!Closed)
                {
                    if (!ReadFully(hdr, 4)) break;
                    int len = BitConverter.ToInt32(hdr, 0);
                    if (len <= 0 || len > MaxMessage) break;
                    byte[] m = new byte[len];
                    if (!ReadFully(m, len)) break;
                    BytesIn += len + 4;
                    lock (gate) inbox.Enqueue(m);
                }
            }
            catch { }
            Closed = true;
            wake.Set();
        }

        bool ReadFully(byte[] buf, int n)
        {
            int got = 0;
            while (got < n)
            {
                int r = sock.Receive(buf, got, n - got, SocketFlags.None);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        void WriteLoop()
        {
            byte[] buf = new byte[8192];
            try
            {
                while (!Closed)
                {
                    wake.WaitOne(250);
                    int n = Take(ref buf);
                    if (n > 0) Push(buf, n);
                }
            }
            catch { }
            Closed = true;
        }

        int Take(ref byte[] buf)
        {
            lock (sendGate)
            {
                int n = pendN;
                if (n == 0) return 0;
                if (buf.Length < n) buf = new byte[pend.Length];
                Buffer.BlockCopy(pend, 0, buf, 0, n);
                pendN = 0;
                return n;
            }
        }

        void Push(byte[] buf, int n)
        {
            lock (sockGate)
            {
                int sent = 0;
                while (sent < n) sent += sock.Send(buf, sent, n - sent, SocketFlags.None);
                BytesOut += n;
            }
        }

        public bool TryRead(out byte[] m)
        {
            lock (gate)
            {
                if (inbox.Count > 0) { m = inbox.Dequeue(); return true; }
            }
            m = null;
            return false;
        }

        /// <summary>Queues a message. Nothing leaves until Flush, so one tick's messages travel together.</summary>
        public void Send(MsgW w)
        {
            if (Closed || w.N <= 0) return;
            lock (sendGate)
            {
                int total = w.N + 4;
                if (pendN + total > MaxPending) { Closed = true; return; }      // the other side has stopped reading
                if (pend.Length < pendN + total) Array.Resize(ref pend, Math.Max(pend.Length * 2, pendN + total));
                pend[pendN] = (byte)w.N; pend[pendN + 1] = (byte)(w.N >> 8); pend[pendN + 2] = (byte)(w.N >> 16); pend[pendN + 3] = (byte)(w.N >> 24);
                Buffer.BlockCopy(w.B, 0, pend, pendN + 4, w.N);
                pendN += total;
            }
        }

        public void Flush() { if (pendN > 0) wake.Set(); }

        public void Close()
        {
            if (!Closed)
            {
                // a last word (such as the reason for a refusal) should still get out
                try { byte[] buf = new byte[8192]; int n = Take(ref buf); if (n > 0) { sock.SendTimeout = 600; Push(buf, n); } } catch { }
            }
            Closed = true;
            wake.Set();
            try { sock.Shutdown(SocketShutdown.Both); } catch { }
            try { sock.Close(); } catch { }
        }
    }
}
