using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static System.String;

namespace query.client
{
    public class QueryClient
    {
        public HttpClient Client { get; set; }
        public string Database { get; set; }

        public string Collection { get; set; }

        public QueryClient(HttpClient client, string database, string collection)
        {
            Database = database;
            Collection = collection;
            Client = client;
        }

        public async Task<(bool, string)> Find(string query)
        {
            try
            {
                if (IsNullOrEmpty(query)) return (false, "empty query");

                var kv = new Dictionary<string, string> {
                    { "database", Database },
                    { "collection", Collection },
                    { "query", query }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("query", content);

                return await getResponse(response);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> FindAndProject(string query, string project)
        {
            try
            {
                if (IsNullOrEmpty(query)) return (false, "empty query");

                var kv = new Dictionary<string, string> {
                    { "database", Database },
                    { "collection", Collection },
                    { "query", query },
                    { "project", project }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("queryandproject", content);

                return await getResponse(response);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> Register(string doc)
        {
            try
            {
                if (IsNullOrEmpty(doc)) return (false, "empty document");

                var kv = new Dictionary<string, string> {
                    { "database", Database },
                    { "collection", Collection },
                    { "query", doc }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("register", content);

                return await getResponse(response);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> Delete(string filter)
        {
            try
            {
                if (IsNullOrEmpty(filter)) return (false, "empty filter");

                var kv = new Dictionary<string, string> {
                    { "database", Database },
                    { "collection", Collection },
                    { "query", filter }
                };

                var content = new StringContent(JsonConvert.SerializeObject(kv), Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("delete", content);

                return await getResponse(response);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool, string)> getResponse(HttpResponseMessage response)
        {
            string r;

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                r = result;
            }
            else
            {
                r = response.StatusCode.ToString();
            }

            return (response.IsSuccessStatusCode, r);
        }
    }
}
