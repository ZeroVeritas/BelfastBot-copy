using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace AnimeApi
{
    public static class MalClient
    {
        public static readonly Uri BaseUri = new Uri("https://api.jikan.moe/v3");

        public static async Task<AnimeResult> GetDetailedAnimeResultsAsync(ulong id)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                string json = await httpClient.GetStringAsync($"{BaseUri}/anime/{id}");

                dynamic obj = JObject.Parse(json);

                dynamic jsonResult = (dynamic)obj.ToObject<dynamic>();

                dynamic[] studios = (dynamic[])jsonResult.studios.ToObject<dynamic[]>();
                dynamic studio = studios.ElementAtOrDefault(0);

                return new AnimeResult
                {
                    MalId = jsonResult.mal_id,
                    Status = jsonResult.status,
                    Title = jsonResult.title,
                    Synopsis = jsonResult.synopsis,
                    Type = jsonResult.type,
                    Episodes = jsonResult.episodes.ToObject<int?>(),
                    Score = jsonResult.score.ToObject<float?>(),
                    ImageUrl = jsonResult.image_url,
                    SiteUrl = jsonResult.url,
                    //Detailed
                    Source = jsonResult.source,
                    Duration = jsonResult.duration,
                    Broadcast = jsonResult.broadcast,
                    TrailerUrl = jsonResult.trailer_url,
                    Studio = studio?.name,
                    StudioUrl = studio?.url,
                    ApiType = ApiType.MyAnimeList,
                };
            }
        }
        public static async Task<MangaResult> GetDetailedMangaResultsAsync(ulong id)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                string json = await httpClient.GetStringAsync($"{BaseUri}/manga/{id}");

                dynamic obj = JObject.Parse(json);

                dynamic jsonResult = (dynamic)obj.ToObject<dynamic>();

                dynamic[] authors = (dynamic[])jsonResult.authors.ToObject<dynamic[]>();

                return new MangaResult
                {
                    MalId = jsonResult.mal_id,
                    Status = jsonResult.status,
                    Title = jsonResult.title,
                    Synopsis = jsonResult.synopsis,
                    Type = jsonResult.type,
                    Chapters = jsonResult.chapters.ToObject<int?>(),
                    Volumes = jsonResult.volumes.ToObject<int?>(),
                    Score = jsonResult.score.ToObject<float?>(),
                    ImageUrl = jsonResult.image_url,
                    SiteUrl = jsonResult.url,
                    //Detailed
                    Staff = authors.Select(author => new Staff()
                    {
                        Name = (string)author.name.ToObject<string>(),
                        SiteUrl = (string)author.url.ToObject<string>()
                    }).ToArray(),
                    ApiType = ApiType.MyAnimeList,
                };
            }
        }

        public static async Task<ulong[]> GetAnimeIdAsync(string name)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                string json = await httpClient.GetStringAsync($"{BaseUri}/search/anime?q={name}&limit=10");

                dynamic obj = JObject.Parse(json);

                dynamic[] jsonResults = (dynamic[])obj.results.ToObject<dynamic[]>();

                ulong[] results = new ulong[jsonResults.Length];
                int i = 0;
                foreach (dynamic jsonResult in jsonResults)
                {

                    results[i] = jsonResult.mal_id;
                    i++;
                }
                return results;
            }
        }
        public static async Task<ulong[]> GetMangaIdAsync(string name)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                string json = await httpClient.GetStringAsync($"{BaseUri}/search/manga?q={name}&limit=10");

                dynamic obj = JObject.Parse(json);

                dynamic[] jsonResults = (dynamic[])obj.results.ToObject<dynamic[]>();

                ulong[] results = new ulong[jsonResults.Length];
                int i = 0;
                foreach (dynamic jsonResult in jsonResults)
                {
                    results[i] = jsonResult.mal_id;
                    i++;
                }
                return results;
            }
        }
    }
}