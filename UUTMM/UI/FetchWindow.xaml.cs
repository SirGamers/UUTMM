﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Input;

namespace UUTMM
{
    /// <summary>
    /// Interaction logic for EditWindow.xaml
    /// </summary>
    public partial class FetchWindow : Window
    {
        public bool success;
        public Mod _mod;
        public FetchWindow(Mod mod)
        {
            InitializeComponent();
            _mod = mod;
            Title = $"Fetch Metadata for {_mod.name}";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private Uri CreateUri(string url)
        {
            Uri uri;
            if ((Uri.TryCreate(url, UriKind.Absolute, out uri) || Uri.TryCreate("http://" + url, UriKind.Absolute, out uri)) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Use validated URI here
                string host = uri.DnsSafeHost;
                if (uri.Segments.Length != 3)
                    return null;
                switch (host)
                {
                    case "www.gamebanana.com":
                    case "gamebanana.com":
                        return uri;
                }
            }
            return null;
        }

        private async void Fetch()
        {
            Uri url = CreateUri(UrlBox.Text);
            if (url != null)
            {
                try
                {
                    var MOD_TYPE = char.ToUpper(url.Segments[1][0]) + url.Segments[1].Substring(1, url.Segments[1].Length - 3);
                    var MOD_ID = url.Segments[2];
                    var client = new HttpClient();
                    var requestUrl = $"https://gamebanana.com/apiv6/{MOD_TYPE}/{MOD_ID}?_csvProperties=_aSubmitter,_sDescription,_aPreviewMedia,_sProfileUrl,_sName," +
                        $"_aSuperCategory,_aCategory,_tsDateUpdated";
                    string responseString = await client.GetStringAsync(requestUrl);
                    var record = JsonSerializer.Deserialize<GameBananaAPIV4>(responseString);
                    var metadata = new Metadata();
                    metadata.submitter = record.Owner.Name;
                    metadata.description = record.Description;
                    metadata.preview = record.Image;
                    metadata.homepage = record.Link;
                    metadata.avi = record.Owner.Avatar;
                    metadata.upic = record.Owner.Upic;
                    metadata.cat = record.CategoryName;
                    metadata.caticon = record.Category.Icon;
                    metadata.lastupdate = record.DateUpdated;
                    string metadataString = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText($@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{_mod.name}{Global.s}mod.json", metadataString);


                    if (!_mod.name.Equals(record.Title, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var oldDirectory = $"{Global.assemblyLocation}{Global.s}Mods{Global.s}{_mod.name}";
                        var newDirectory = $"{Global.assemblyLocation}{Global.s}Mods{Global.s}{record.Title}";
                        if (!Directory.Exists(newDirectory))
                        {
                            try
                            {
                                Directory.Move(oldDirectory, newDirectory);
                                Close();
                            }
                            catch (Exception ex)
                            {
                                Global.logger.WriteLine($"Couldn't rename {oldDirectory} to {newDirectory} ({ex.Message})", LoggerType.Error);
                            }
                        }
                        else
                            Global.logger.WriteLine($"{newDirectory} already exists", LoggerType.Error);
                    }

                    var index = Global.config.ModList.ToList().FindIndex(x => x.name == _mod.name);
                    Global.config.ModList[index].preview = record.Image;
                    Global.config.ModList[index].name = record.Title;
                    Global.ModList = Global.config.ModList;
                    success = true;
                    Close();
                }
                catch (Exception ex)
                {
                    Global.logger.WriteLine(ex.Message, LoggerType.Error);
                }
            }
            else
                Global.logger.WriteLine($"{UrlBox.Text} is invalid. The url should have the following format: https://gamebanana.com/<Mod Category>/<Mod ID>", LoggerType.Error);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Fetch();
        }

        private void UrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                Fetch();
        }
    }
}
