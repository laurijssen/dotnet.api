using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace graphicstransform.client
{
    public class GraphicsClient
    {
        private class Rectangle
        {
            public int X { get; set; }
            public int Y { get; set; }

            [JsonProperty("Width")] public int W { get; set; }

            [JsonProperty("Height")] public int H { get; set; }

            public void Deconstruct(out int x, out int y, out int w, out int h) { x = X; y = Y; w = W; h = H; }
        }

        public HttpClient Client { get; set; }

        public GraphicsClient(HttpClient client)
        {
            Client = client;
        }

        public async Task<(bool, byte[])> Resize(float wfactor, float hfactor, byte[] data)
        {
            var kv = new Dictionary<string, string> {
                    { "wfactor", wfactor.ToString()},
                    { "hfactor", hfactor.ToString() },
                    { "data",  Convert.ToBase64String(data) }
                };

            var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("graphicstransform/resize", content);

            return await getImageData(response);
        }

        public async Task<(bool, byte[])> RotateFlip(int rotate, int fliptype, byte[] data)
        {
            var kv = new Dictionary<string, string> {
                    { "rotate", rotate.ToString()},
                    { "fliptype", fliptype.ToString() },
                    { "data",  Convert.ToBase64String(data) }
                };

            var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("rotateflip", content);

            return await getImageData(response);
        }

        public async Task<(bool, byte[])> DrawImageOnImage(byte[] dstData, byte[] srcData, int x, int y, int w, int h)
        {
            var kv = new Dictionary<string, string> {
                    { "x", x.ToString()},
                    { "y", y.ToString() },
                    { "w", w.ToString() },
                    { "h", h.ToString() },
                    { "dstData",  Convert.ToBase64String(dstData) },
                    { "srcData",  Convert.ToBase64String(srcData) },
                };

            var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("drawimageonimage", content);

            return await getImageData(response);
        }

        public async Task<(int x, int y, int w, int h)> ColorkeyRect(byte r, byte g, byte b, byte[] data)
        {
            if (data == null) return (0, 0, 0, 0);

            var kv = new Dictionary<string, string> {
                    { "r", r.ToString()},
                    { "g", g.ToString() },
                    { "b", b.ToString() },
                    { "data",  Convert.ToBase64String(data) },
                };

            var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("colorkeyrect", content);

            var (X, Y, W, H) = await getResult<Rectangle>(response);

            return (X, Y, W, H);
        }

        public async Task<List<(int x, int y, int w, int h)>> ColorkeyRectAlpha(byte[] data)
        {
            var kv = new Dictionary<string, string> {
                    { "data",  Convert.ToBase64String(data) },
                };

            var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync("colorkeyrectalpha", content);

            var list = await getResult<List<Rectangle>>(response);

            return list.ConvertAll(r => (r.X, r.Y, r.W, r.H));
        }

        private async Task<T> getResult<T>(HttpResponseMessage response)
        {
            string content;

            if (response.IsSuccessStatusCode)
            {
                content = await response.Content.ReadAsStringAsync();

                var obj = JsonConvert.DeserializeObject<T>(content);

                if (obj == null)
                    throw new Exception("invalid content in response");

                return obj;
            }
            else
            {
                throw new Exception(response.StatusCode.ToString());
            }
        }

        private async Task<(bool, byte[])> getImageData(HttpResponseMessage response)
        {
            byte[] b64;

            if (response.IsSuccessStatusCode)
            {
                b64 = Convert.FromBase64String(await response.Content.ReadAsStringAsync());
            }
            else
            {
                b64 = Encoding.ASCII.GetBytes(response.StatusCode.ToString());
            }

            return (response.IsSuccessStatusCode, b64);
        }
    }
}