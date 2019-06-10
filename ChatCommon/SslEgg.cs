// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;


namespace ChatCommon
{
    /// <summary>
    /// This class was created purely to get RemoteEndpoint info, which is not accessible through SslStream.
    /// It ties the SslStream with its TcpClient.
    /// It doesn't close the stream or perform any other resource management.
    /// </summary>
    public class SslEgg
    {
        // public properties
        public TcpClient TcpSocket { get; private set; } = null;
        public SslStream SslStream { get; private set; } = null;  // set by AuthenticateAsServerAsync or AuthenticateAsClient


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tcpClient">A TcpClient instance to tie to the future SslStream. It shall be connected before calling this constructor.</param>
        public SslEgg(TcpClient tcpClient)
        {
            TcpSocket = tcpClient;
        }


        public virtual async Task AuthenticateAsServerAsync(string certificatePath)
        {
            if (SslStream != null)
                throw new InvalidOperationException("SSL Stream was not null.");
            SslStream = await ConvertToSslServerAsync(certificatePath, TcpSocket);
        }

        public virtual void AuthenticateAsClient(string targetHostCert)
        {
            if (SslStream != null)
                throw new InvalidOperationException("SSL Stream was not null.");
            SslStream = ConvertToSslClient(targetHostCert, TcpSocket);
        }

        public static async Task<SslStream> ConvertToSslServerAsync(string certificatePath, TcpClient tcpClient)
        {
            // load our certificate
            X509Certificate2 cert = null;
            cert = new X509Certificate2(certificatePath);
            // convert to ssl stream
            SslStream sslClient = new SslStream(tcpClient.GetStream(), false);
            // authenticate ourself
            await sslClient.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12, false);

            return sslClient;
        }


        public static SslStream ConvertToSslClient(string targetHostCert, TcpClient tcpConnection)
        {
            // convert to ssl stream
            SslStream sslServer = new SslStream(tcpConnection.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCert), null);
            // authenticate server
            sslServer.AuthenticateAsClient(targetHostCert);

            return sslServer;
        }

        public static bool ValidateServerCert(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            Logging.WriteLine(String.Format("Certificate error: {0}", sslPolicyErrors));

            #warning Don't use in production!
            return true;  // dev environment :)
        }
    }
}
