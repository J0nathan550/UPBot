using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UPBot.src.DataClasses;

namespace UPBot.src.Commands;
/// <summary>
/// Цeather command, allows users to get information about the weather in their city
/// Made with help of weatherapi.com
/// Information can be false from time to time, but it works. 
/// Author: J0nathan550
/// </summary>
public class Weather : ApplicationCommandModule
{
    [SlashCommand("weather", "Get weather information from any city")]
    public static async Task WeatherCommand(InteractionContext ctx, [Option("city", "Weather about the city")] string city = null)
    {
        try
        {

            if (string.IsNullOrWhiteSpace(Configs.WeatherAPIKey))
            {
                Utils.Log($"Weather API Key is not defined", null);
                await ctx.CreateResponseAsync(Utils.GenerateErrorAnswer(ctx.Guild.Name, "Weather", "Weather API Key is not specified"));
                return;
            }


            if (city == null)
            {
                DiscordEmbedBuilder discordEmbed = new()
                {
                    Title = "Error!",
                    Description = "Looks like you typed a wrong city, or you typed nothing.",
                    Color = DiscordColor.Red
                };
                await ctx.CreateResponseAsync(discordEmbed.Build());
                return;
            }
            Utils.Log($"Weather executed by {ctx.User} command: Trying to get information from city: {city}", null);

            using HttpClient response = new();
            string json;
            try
            {
                json = response.GetStringAsync($"https://api.weatherapi.com/v1/forecast.json?key={Configs.WeatherAPIKey}&q={city}&days=3&aqi=yes&alerts=yes").Result;
            }
            catch (Exception)
            {
                await ctx.CreateResponseAsync($"Double check if the city _{city}_ is correct.");
                return;
            }
            WeatherData data = JsonConvert.DeserializeObject<WeatherData>(json);

            if (data == null)
            {
                DiscordEmbedBuilder discordEmbed = new()
                {
                    Title = "Error!",
                    Description = "There was a problem in getting weather information, try again.",
                    Color = DiscordColor.Red
                };
                await ctx.CreateResponseAsync(discordEmbed.Build());
                return;
            }

            DiscordColor orangeColor = new("#fc7f03");

            DiscordEmbedBuilder discordEmbedBuilder = new()
            {
                Title = $"Weather information - {city}",
                Timestamp = DateTime.Parse(data.Current.LastUpdated),
                Color = orangeColor,
                Footer = new DiscordEmbedBuilder.EmbedFooter()
                {
                    Text = "Last weather update: ",
                    IconUrl = "https://media.discordapp.net/attachments/1137667651326447726/1137668268426002452/cloudy.png"
                },
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                    Url = $"https:{data.Current.Condition.Icon}"
                },
            };

            discordEmbedBuilder.AddField(":cityscape: City", $"{data.Location.Name}", true);
            discordEmbedBuilder.AddField(":map: Region", $"{(string.IsNullOrWhiteSpace(data.Location.Region) ? data.Location.Name : data.Location.Region)}", true);
            discordEmbedBuilder.AddField(":globe_with_meridians: Country", $"{(string.IsNullOrWhiteSpace(data.Location.Country) ? data.Location.Name : data.Location.Country)}", true);

            discordEmbedBuilder.AddField(":map: Coordinates", $"ϕ{data.Location.Lat} λ{data.Location.Lon}", true);
            discordEmbedBuilder.AddField($":timer: Timezone", $"{(string.IsNullOrWhiteSpace(data.Location.TzId) ? "__undefined__" : data.Location.TzId)}", true);
            discordEmbedBuilder.AddField(":clock1:Time", $"{data.Location.Localtime}", true);

            discordEmbedBuilder.AddField(":thermometer: Temperature", $"{data.Current.TempC}°C/{data.Current.TempF}°F", true);
            discordEmbedBuilder.AddField(":thermometer: Feels like", $"{data.Current.FeelslikeC}°C/{data.Current.FeelslikeF}°F", true);
            discordEmbedBuilder.AddField(":sunny: Condition", $"{(string.IsNullOrWhiteSpace(data.Current.Condition.Text) ? "__undefined__" : data.Current.Condition.Text)}", true);

            discordEmbedBuilder.AddField(":leaves: Wind speed", $"{data.Current.WindKph}Kph/{data.Current.WindMph}Mph", true);
            discordEmbedBuilder.AddField(":triangular_ruler: Wind angle", $"{data.Current.WindDegree}°", true);
            discordEmbedBuilder.AddField(":straight_ruler: Wind dir", $"{(string.IsNullOrWhiteSpace(data.Current.WindDir) ? "__undefined__" : data.Current.WindDir)}", true);

            discordEmbedBuilder.AddField(":cloud: Clouds", $"{data.Current.Cloud}", true);
            discordEmbedBuilder.AddField(":droplet: Precip", $"{data.Current.PrecipMm}mm/{data.Current.PrecipIn}in", true);
            discordEmbedBuilder.AddField(":cloud_rain: Humidity", $"{data.Current.Humidity}%", true);

            discordEmbedBuilder.AddField(":beach_umbrella: UVs", $"{data.Current.Uv}", true);
            discordEmbedBuilder.AddField(":compression: Pressure", $"{data.Current.PressureMb}Mb/{data.Current.PressureIn}In", true);
            discordEmbedBuilder.AddField(":railway_track: Visibility", $"{data.Current.VisKm}Km/{data.Current.VisMiles}M", true);

            discordEmbedBuilder.AddField("Forecast", "==========================================================", false);
            List<string> convertedForecastStrings = [];
            for (int i = 0; i < data.Forecast.Forecastday.Count; i++)
            {
                convertedForecastStrings.Add(
                    $"Condition: {data.Forecast.Forecastday[i].Day.Condition.Text} :sunny:\n" +
                    $"Max: {data.Forecast.Forecastday[i].Day.MaxtempC}C/{data.Forecast.Forecastday[i].Day.MaxtempF}F\n" +
                    $"Min: {data.Forecast.Forecastday[i].Day.MintempC}C/{data.Forecast.Forecastday[i].Day.MintempF}F\n" +
                    $"Avg.: {data.Forecast.Forecastday[i].Day.AvgtempC}C/{data.Forecast.Forecastday[i].Day.AvgtempF}F\n" +
                    $"Will it rain?: {(data.Forecast.Forecastday[i].Day.DailyWillItRain == 1 ? "Yes :cloud_rain:" : "No :sunny:")} \n" +
                    $"Chance of rain: {data.Forecast.Forecastday[i].Day.DailyChanceOfRain}%\n" +
                    $"Avg. Humidity: {data.Forecast.Forecastday[i].Day.Avghumidity}\n" +
                    $"Precip: {data.Forecast.Forecastday[i].Day.TotalprecipMm}mm/{data.Forecast.Forecastday[i].Day.TotalprecipIn}in\n" +
                    $"Will it snow?: {(data.Forecast.Forecastday[i].Day.DailyWillItSnow == 1 ? "Yes :cloud_snow:" : "No :sunny:")}\n" +
                    $"Chance of snow: {data.Forecast.Forecastday[i].Day.DailyChanceOfSnow}%\n" +
                    $"Total snow: {data.Forecast.Forecastday[i].Day.TotalsnowCm}\n" +
                    $"Ultraviolet index: {data.Forecast.Forecastday[i].Day.Uv} :beach_umbrella:");
                discordEmbedBuilder.AddField($"Date: {data.Forecast.Forecastday[i].Date}", convertedForecastStrings[i], true);
            }
            discordEmbedBuilder.AddField("Astronomic Info",
                $"Sunrise will be: {data.Forecast.Forecastday[0].Astro.Sunrise}     :sunrise:\n" +
                $"Sunset will be: {data.Forecast.Forecastday[0].Astro.Sunset}       :city_sunset:\n" +
                $"Moonrise will be: {data.Forecast.Forecastday[0].Astro.Moonrise}   :full_moon:\n" +
                $"Moonset will be: {data.Forecast.Forecastday[0].Astro.Moonset}     :crescent_moon: \n" +
                $"Moon phase: {data.Forecast.Forecastday[0].Astro.MoonPhase}        :full_moon:\n" +
                $"Moon illumination: {data.Forecast.Forecastday[0].Astro.MoonIllumination}         :bulb:\n" +
                $"Is moon up?: {(data.Forecast.Forecastday[0].Astro.IsMoonUp == 1 ? "Yes" : "No")} :full_moon:\n" +
                $"Is sun up?: {(data.Forecast.Forecastday[0].Astro.IsSunUp == 1 ? "Yes" : "No")}   :sunny:", false);
            await ctx.CreateResponseAsync(discordEmbedBuilder.Build());
        }
        catch (Exception ex)
        {
            Utils.Log($"Weather error command:\nMessage: {ex.Message}\nStacktrace:{ex.StackTrace}", null);
            DiscordEmbedBuilder discordEmbed = new()
            {
                Title = "Error!",
                Description = $"There was a fatal error in executing weather command.\nMessage: {ex.Message}\nStacktrace: {ex.StackTrace}",
                Color = DiscordColor.Red
            };
            await ctx.CreateResponseAsync(discordEmbed.Build());
        }
    }
}