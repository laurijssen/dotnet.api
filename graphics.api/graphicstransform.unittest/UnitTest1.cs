using FluentAssertions;
using graphicstransform.client;
using graphicstransform.service;
using Moq;
using Moq.Protected;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

using static System.Convert;

namespace graphicstransform.unittest
{
    public class UnitTest1
    {
        [Fact]
        public async void TestRealClient()
        {
            if (OperatingSystem.IsWindows())
            {
                //Uri uri = new Uri("http://w2k22apps04:32006");

                Uri uri = new Uri("http://ubdock05:32006");
                var httpClient = new HttpClient() { BaseAddress = uri };

                var client = new GraphicsClient(httpClient);

                var b = File.ReadAllBytes("d:\\temp\\test.bmp");

                var ret = await client.Resize(2.0f, 2.0f, b);

                if (ret.Item1)
                    File.WriteAllBytes("d:\\temp\\test7.bmp", ret.Item2);

                ret = await client.RotateFlip(90, 1, b);

                if (ret.Item1)
                    File.WriteAllBytes("d:\\temp\\test8.bmp", ret.Item2);

                var duck = File.ReadAllBytes("d:\\temp\\duck.jpg");
                var roll = File.ReadAllBytes("d:\\temp\\test.bmp");

                ret = await client.DrawImageOnImage(duck, roll, 300, 200, 300, 300);

                if (ret.Item1)
                    File.WriteAllBytes("d:\\temp\\test7.jpg", ret.Item2);

                b = File.ReadAllBytes("D:\\git\\graphics.api\\graphicstransform.unittest\\transparent.png");

                var rect = await client.ColorkeyRect(0, 0, 0, b);

                var rects = await client.ColorkeyRectAlpha(b);

                foreach (var r in rects)
                {
                }
            }
        }

            [Fact]
            public void TestClientConnectionErrors()
            {
                var codes = new System.Net.HttpStatusCode[]
                {
                System.Net.HttpStatusCode.BadRequest,
                System.Net.HttpStatusCode.Forbidden,
                System.Net.HttpStatusCode.NotFound,
                };

                Array.ForEach(codes, async (c) =>
                {
                    var client = GetMockClient(string.Empty, c);

                    var res = await client.Resize(1.0f, 1.0f, new byte[1] { 0x00 });

                    res.Item1.Should().BeFalse();
                    res.Item2.Should().Equal(Encoding.ASCII.GetBytes(c.ToString()));
                });
            }

            [Fact]
            public void TestClientConnectionErrorsColorkeys()
            {
                var codes = new System.Net.HttpStatusCode[]
                {
                System.Net.HttpStatusCode.BadRequest,
                System.Net.HttpStatusCode.Forbidden,
                System.Net.HttpStatusCode.NotFound,
                };

                Array.ForEach(codes, async (c) =>
                {
                    var client = GetMockClient(string.Empty, c);

                    await Assert.ThrowsAsync<Exception>(() => { return client.ColorkeyRect(0, 0, 0, new byte[] { 0x1, 0x2 }); });
                    await Assert.ThrowsAsync<Exception>(() => { return client.ColorkeyRectAlpha(new byte[] { 0x1, 0x2 }); });
                });
            }

            [Fact]
            public async void TestRotateFlipInvalidArguments()
            {
                GraphicsServer server = new();

                await Assert.ThrowsAsync<ArgumentException>(() => { return server.RotateFlip(999, 0, ""); });
                await Assert.ThrowsAsync<ArgumentException>(() => { return server.RotateFlip(90, 999, ""); });
                await Assert.ThrowsAsync<ArgumentException>(() => { return server.RotateFlip(90, 0, "INVALID BASE64"); });
                await Assert.ThrowsAsync<UnknownImageFormatException>(() => { return server.RotateFlip(90, 0, ToBase64String(Encoding.ASCII.GetBytes("UNKNOWN IMAGE FORMAT EXCEPTION"))); });
            }

            [Fact]
            public async void TestImageOnImageInvalidArguments()
            {
                GraphicsServer server = new();

                var b64 = ToBase64String(new byte[] { 0x1, 0x2 });
                var data = new byte[] { 0x42, 0x4D, 0x46, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x36, 0x0, 0x0, 0x0, 0x28, 0x0, 0x0, 0x0, 0x2,
                                       0x0, 0x0, 0x0, 0x2, 0x0, 0x0, 0x0, 0x1, 0x0, 0x18, 0x0, 0x0, 0x0, 0x0, 0x0, 0x10, 0x0, 0x0, 0x0, 0xC4,
                                       0xE, 0x0, 0x0, 0xC4, 0xE, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                                       0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

                var valid = ToBase64String(data);

                await Assert.ThrowsAsync<ArgumentException>(() => { return server.DrawImageOnImage("", "VALID", new()); });
                await Assert.ThrowsAsync<ArgumentException>(() => { return server.DrawImageOnImage("VALID", "", new()); });
                await Assert.ThrowsAsync<ArgumentException>(() => { return server.DrawImageOnImage("NONBASE64", b64, new()); });
                await Assert.ThrowsAsync<ArgumentException>(() => { return server.DrawImageOnImage(valid, "NONBASE64", new()); });
                await Assert.ThrowsAsync<UnknownImageFormatException>(() => { return server.DrawImageOnImage(b64, valid, new()); });
                await Assert.ThrowsAsync<UnknownImageFormatException>(() => { return server.DrawImageOnImage(valid, b64, new()); });
            }

            [Fact]
            public async void TestRotateFlip()
            {
                GraphicsServer server = new();

                var data = new byte[] { 0x42, 0x4D, 0x46, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x36, 0x0, 0x0, 0x0, 0x28, 0x0, 0x0, 0x0, 0x2,
                                       0x0, 0x0, 0x0, 0x2, 0x0, 0x0, 0x0, 0x1, 0x0, 0x18, 0x0, 0x0, 0x0, 0x0, 0x0, 0x10, 0x0, 0x0, 0x0, 0xC4,
                                       0xE, 0x0, 0x0, 0xC4, 0xE, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                                       0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

                var transformed = await server.RotateFlip(0, 0, ToBase64String(data));

                var newdata = FromBase64String(transformed);

                Assert.Equal(data.Count(), newdata.Count());
            }

            [Fact]
            public async void TestResize()
            {
                GraphicsServer server = new();

                byte[] data = new byte[] { 0x42, 0x4D, 0x46, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x36, 0x0, 0x0, 0x0, 0x28, 0x0, 0x0, 0x0, 0x2,
                                       0x0, 0x0, 0x0, 0x2, 0x0, 0x0, 0x0, 0x1, 0x0, 0x18, 0x0, 0x0, 0x0, 0x0, 0x0, 0x10, 0x0, 0x0, 0x0, 0xC4,
                                       0xE, 0x0, 0x0, 0xC4, 0xE, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                                       0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

                var transformed = await server.Resize(2, 2, ToBase64String(data));

                var newdata = FromBase64String(transformed);

                Assert.True(102 == newdata.Count());
            }

            [Fact]
            public async void TestFindColorKey()
            {
                GraphicsServer server = new();

                byte[] data = File.ReadAllBytes("bw.png");

                var rect = server.ColorkeyRect(255, 255, 255, ToBase64String(data));

                byte[] greendata = File.ReadAllBytes("green.png");

                var newImage = await server.DrawImageOnImage(ToBase64String(data), ToBase64String(greendata),
                                     new Rectangle { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height });

                FromBase64String(newImage).Count().Equals(data.Length);

                var newRect = server.ColorkeyRect(0, 255, 0, newImage);

                newRect.Should().Be(rect);
            }

            [Fact]
            public async void TestFindColorKeyAlpha()
            {
                GraphicsServer server = new();

                var data = File.ReadAllBytes("transparent.png");

                var rects = server.ColorkeyRectAlpha(ToBase64String(data));

                rects.Count().Should().Be(6);

                byte[] greendata = File.ReadAllBytes("green.png"),
                       imageData = data;

                foreach (var rect in rects)
                {
                    var newImage = await server.DrawImageOnImage(ToBase64String(imageData), ToBase64String(greendata),
                                         new Rectangle { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height });

                    imageData = FromBase64String(newImage);
                }

                var imageSharp = Image.Load<Rgba32>(imageData);

                foreach (var rect in rects)
                {
                    imageSharp[rect.X, rect.Y].G.Should().Be(255);
                    imageSharp[rect.X + rect.Width - 1, rect.Y + rect.Height - 1].G.Should().Be(255);
                }
            }

            [Fact]
            public void TestFindColorKeyAlphaNoSquare()
            {
                GraphicsServer server = new();

                byte[] data = File.ReadAllBytes("green.png");

                var rects = server.ColorkeyRectAlpha(ToBase64String(data));

                rects.Count().Should().Be(0);
            }

            [Fact]
            public void TestFindColorKeyAlphaInvalidArguments()
            {
                GraphicsServer server = new();

                Assert.Throws<ArgumentException>(() => { _ = server.ColorkeyRectAlpha(""); });
                Assert.Throws<ArgumentException>(() => { _ = server.ColorkeyRectAlpha("INVALID BASE64"); });
            }

            [Fact]
            public void TestFindColorKeyInvalidArguments()
            {
                GraphicsServer server = new();

                Assert.Throws<ArgumentException>(() => { _ = server.ColorkeyRect(0, 0, 0, ""); });
                Assert.Throws<ArgumentException>(() => { _ = server.ColorkeyRect(0, 0, 0, "INVALID BASE64"); });
            }

            [Fact]
            public void TestFindColorKeyNoSquare()
            {
                GraphicsServer server = new();

                byte[] data = File.ReadAllBytes("green.png");

                var rect = server.ColorkeyRect(255, 255, 255, ToBase64String(data));

                rect.Should().Be(new Rectangle(0, 0, 0, 0));
            }

            private GraphicsClient GetMockClient(string content, System.Net.HttpStatusCode code)
            {
                var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);

                handlerMock
                   .Protected()
                   .Setup<Task<HttpResponseMessage>>(
                      "SendAsync",
                      ItExpr.IsAny<HttpRequestMessage>(),
                      ItExpr.IsAny<CancellationToken>()
                   )
                   .ReturnsAsync(new HttpResponseMessage()
                   {
                       StatusCode = code,
                       Content = new StringContent(content),
                   })
                   .Verifiable();

                Uri uri = new Uri("https://api.test.com");
                var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = uri };
                return new GraphicsClient(httpClient);
            }
        }
    }