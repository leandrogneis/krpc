using System;
using System.IO;
using System.Linq;
using Google.Protobuf;
using KRPC.Server;
using KRPC.Server.ProtocolBuffers;
using KRPC.Service.Messages;
using NUnit.Framework;

namespace KRPC.Test.Server.ProtocolBuffers
{
    [TestFixture]
    public class RPCStreamTest
    {
        Request expectedRequest;
        byte[] requestBytes;
        Response expectedResponse;
        byte[] responseBytes;

        [SetUp]
        public void SetUp ()
        {
            // Create a request object and get the binary representation of it
            expectedRequest = new Request ();
            expectedRequest.Service = "TestService";
            expectedRequest.Procedure = "ProcedureNoArgsNoReturn";
            using (var stream = new MemoryStream ()) {
                var codedStream = new CodedOutputStream (stream);
                codedStream.WriteInt32 (expectedRequest.ToProtobufMessage ().CalculateSize ());
                expectedRequest.ToProtobufMessage ().WriteTo (codedStream);
                codedStream.Flush ();
                requestBytes = stream.ToArray ();
            }

            // Create a response object and get the binary representation of it
            expectedResponse = new Response ();
            expectedResponse.Error = "SomeErrorMessage";
            expectedResponse.Time = 42;
            using (var stream = new MemoryStream ()) {
                var codedStream = new CodedOutputStream (stream);
                codedStream.WriteInt32 (expectedResponse.ToProtobufMessage ().CalculateSize ());
                expectedResponse.ToProtobufMessage ().WriteTo (codedStream);
                codedStream.Flush ();
                responseBytes = stream.ToArray ();
            }
        }

        [Test]
        public void Empty ()
        {
            var byteStream = new TestStream (new MemoryStream ());
            var rpcStream = new RPCStream (byteStream);
            Assert.IsFalse (rpcStream.DataAvailable);
            Assert.Throws<NoRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (0, rpcStream.BytesRead);
            Assert.IsFalse (byteStream.Closed);
        }

        [Test]
        public void ReadSingleRequest ()
        {
            var byteStream = new TestStream (requestBytes);
            var rpcStream = new RPCStream (byteStream);
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (0, rpcStream.BytesRead);
            Assert.IsTrue (rpcStream.DataAvailable);
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (requestBytes.Length, rpcStream.BytesRead);
            Request request = rpcStream.Read ();
            Assert.IsFalse (rpcStream.DataAvailable);
            Assert.Throws<NoRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (expectedRequest.Service, request.Service);
            Assert.AreEqual (expectedRequest.Procedure, request.Procedure);
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (requestBytes.Length, rpcStream.BytesRead);
            Assert.IsFalse (byteStream.Closed);
        }

        [Test]
        public void ReadMultipleRequests ()
        {
            var multipleRequestBytes = new byte [requestBytes.Length * 5];
            for (int i = 0; i < 5; i++)
                Array.Copy (requestBytes, 0, multipleRequestBytes, i * requestBytes.Length, requestBytes.Length);
            var byteStream = new TestStream (multipleRequestBytes);
            var rpcStream = new RPCStream (byteStream);
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (0, rpcStream.BytesRead);
            for (int i = 0; i < 5; i++) {
                Assert.IsTrue (rpcStream.DataAvailable);
                Assert.AreEqual (0, rpcStream.BytesWritten);
                Assert.AreEqual (multipleRequestBytes.Length, rpcStream.BytesRead);
                Request request = rpcStream.Read ();
                if (i < 4)
                    Assert.IsTrue (rpcStream.DataAvailable);
                else {
                    Assert.IsFalse (rpcStream.DataAvailable);
                    Assert.Throws<NoRequestException> (() => rpcStream.Read ());
                }
                Assert.AreEqual (expectedRequest.Service, request.Service);
                Assert.AreEqual (expectedRequest.Procedure, request.Procedure);
                Assert.AreEqual (0, rpcStream.BytesWritten);
                Assert.AreEqual (multipleRequestBytes.Length, rpcStream.BytesRead);
            }
            Assert.IsFalse (byteStream.Closed);
        }

        [Test]
        public void ReadSingleRequestInParts ()
        {
            // Split the message bytes into 3 parts
            Assert.IsTrue (requestBytes.Length > 15);
            var part1 = new byte[4];
            var part2 = new byte[6];
            var part3 = new byte[requestBytes.Length - 10];
            Array.Copy (requestBytes, 0, part1, 0, part1.Length);
            Array.Copy (requestBytes, part1.Length, part2, 0, part2.Length);
            Array.Copy (requestBytes, part1.Length + part2.Length, part3, 0, part3.Length);

            // Write part 1
            var stream = new MemoryStream ();
            stream.Write (part1, 0, part1.Length);
            stream.Seek (0, SeekOrigin.Begin);

            // Read part 1
            var byteStream = new TestStream (stream);
            var rpcStream = new RPCStream (byteStream);
            Assert.IsFalse (rpcStream.DataAvailable);
            Assert.Throws<NoRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (part1.Length, rpcStream.BytesRead);

            // Write part 2
            Assert.AreEqual (part1.Length, stream.Position);
            stream.Write (part2, 0, part2.Length);
            stream.Seek (part1.Length, SeekOrigin.Begin);

            // Read part 2
            Assert.IsFalse (rpcStream.DataAvailable);
            Assert.Throws<NoRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (part1.Length + part2.Length, rpcStream.BytesRead);

            // Write part 3
            Assert.AreEqual (part1.Length + part2.Length, stream.Position);
            stream.Write (part3, 0, part3.Length);
            stream.Seek (-part3.Length, SeekOrigin.Current);

            // Read part 3
            Assert.IsTrue (rpcStream.DataAvailable);
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (part1.Length + part2.Length + part3.Length, rpcStream.BytesRead);
            Request request = rpcStream.Read ();
            Assert.IsFalse (rpcStream.DataAvailable);
            Assert.Throws<NoRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (part1.Length + part2.Length + part3.Length, rpcStream.BytesRead);
            Assert.AreEqual (expectedRequest.Service, request.Service);
            Assert.AreEqual (expectedRequest.Procedure, request.Procedure);
            Assert.IsFalse (byteStream.Closed);
        }

        [Test]
        public void ReadGarbage ()
        {
            var data = new byte [4000];
            var rand = new Random (42);
            rand.NextBytes (data);
            var byteStream = new TestStream (data);
            var rpcStream = new RPCStream (byteStream);
            Assert.Throws<MalformedRequestException> (() => rpcStream.Read ());
            Assert.AreEqual (0, rpcStream.BytesWritten);
            Assert.AreEqual (data.Length, rpcStream.BytesRead);
            Assert.IsTrue (byteStream.Closed);
        }

        [Test]
        public void WriteSingleResponse ()
        {
            var stream = new MemoryStream ();
            var byteStream = new TestStream (null, stream);
            var rpcStream = new RPCStream (byteStream);
            rpcStream.Write (expectedResponse);
            Assert.AreEqual (responseBytes.ToHexString (), stream.ToArray ().ToHexString ());
            Assert.AreEqual (responseBytes.Length, rpcStream.BytesWritten);
            Assert.AreEqual (0, rpcStream.BytesRead);
            Assert.IsFalse (byteStream.Closed);
        }
    }
}