﻿using EverLoader.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EverLoader.Extensions;
using System.Drawing;
using System.Text.RegularExpressions;
using EverLoader.Helpers;
using System.Threading.Tasks;
using TheGamesDBApiWrapper.Models.Enums;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using TheGamesDBApiWrapper.Domain;
using EverLoader.Properties;

namespace EverLoader.Services
{
    public class GamesManager
    {
        private const int MAX_GAME_ID_LENGTH = 16;
        private readonly string APP_GAMES_FOLDER = $"{Constants.APP_ROOT_FOLDER}games\\";
        private const string SUBFOLDER_IMAGES = "images\\";
        private const string SUBFOLDER_IMAGES_SOURCE = "images\\source\\";
        private const string SUBFOLDER_ROM = "rom\\";

        private readonly Dictionary<string, GameInfo> _games = new Dictionary<string, GameInfo>();
        private readonly HashSet<uint> _gameCRCs = new HashSet<uint>(); //used for quick lookup of existing CRCs

        private readonly ITheGamesDBAPI _tgdbApi;
        private readonly RomManager _romManager;
        private readonly ImageManager _imageManager;
        private readonly DownloadManager _downloadManager;
        private readonly AppSettings _appSettings;

        public GamesManager(ITheGamesDBAPI tgdbApi,
            RomManager romManager,
            ImageManager imageManager,
            DownloadManager downloadManager,
            AppSettings appSettings)
        {
            _tgdbApi = tgdbApi;
            _romManager = romManager;
            _imageManager = imageManager;
            _downloadManager = downloadManager;
            _appSettings = appSettings;

            //create games folder directory
            Directory.CreateDirectory(APP_GAMES_FOLDER);
        }

        public IEnumerable<GameInfo> Games => _games.Values;
        public Dictionary<string, GameInfo> GamesDictionary => _games;

        public IEnumerable<ImageInfo> GetGameBoxartImageInfo(string gameId)
        {
            return new[]
            {
                GetGameImageInfo(gameId, ImageType.Large),
                GetGameImageInfo(gameId, ImageType.Medium),
                GetGameImageInfo(gameId, ImageType.Small),
            };
        }

        public ImageInfo GetGameImageInfo(string gameId, ImageType imageType)
        {
            switch (imageType)
            {
                case ImageType.Medium:
                    return new ImageInfo()
                    {
                        //Size = new Size(210, 295),
                        Size = new Size(260, 358),
                        LocalPath = $"{APP_GAMES_FOLDER}{gameId}\\{SUBFOLDER_IMAGES}{gameId}0_hd.png",
                        ImageType = imageType
                    };
                case ImageType.Large:
                    return new ImageInfo()
                    {
                        Size = new Size(474, 666),
                        LocalPath = $"{APP_GAMES_FOLDER}{gameId}\\{SUBFOLDER_IMAGES}{gameId}0_1080.png",
                        ImageType = imageType
                    };
                case ImageType.Banner:
                    return new ImageInfo()
                    {
                        Size = new Size(1920, 551),
                        LocalPath = $"{APP_GAMES_FOLDER}{gameId}\\{SUBFOLDER_IMAGES}{gameId}_gamebanner.png",
                        ImageType = imageType
                    };
                default:
                case ImageType.Small:
                    return new ImageInfo()
                    {
                        Size = new Size(112, 157),
                        LocalPath = $"{APP_GAMES_FOLDER}{gameId}\\{SUBFOLDER_IMAGES}{gameId}0.png",
                        ImageType = imageType
                    };
            }
        }

        public GameInfo GetGameById(string id)
        {
            return _games.TryGetValue(id, out GameInfo value) ? value : null;
        }

        public Platform GetGamePlatform(GameInfo game)
        {
            return game != null ? _appSettings.Platforms.First(p => p.Id == game.romPlatformId) : null;
        }

        public IEnumerable<Platform> GetGamePlatformsByRomExtesion(string extension)
        {
            if (extension == null) return null;
            return _appSettings.Platforms.Where(p => p.RomFileExtensions.Contains(extension.ToLowerInvariant()));
        }

        public async Task SerializeGame(GameInfo game)
        {
            if (string.IsNullOrEmpty(game.romPlatform))
            {
                var platform = _appSettings.Platforms.SingleOrDefault(p => p.Id == game.romPlatformId);
                game.romPlatform = platform?.Name; //updating romPlatform won't trigger update events 
            }

            var gameJson = JsonConvert.SerializeObject(game, Formatting.Indented);
            await File.WriteAllTextAsync($"{APP_GAMES_FOLDER}{game.Id}\\{game.Id}.json", gameJson);
        }

        public string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        public async Task SyncToSd(string sdDrive, string cartName, IProgress<(string, int, int)> progress)
        {
            var selectedGameIds = _games.Values.Where(g => g.IsSelected).Select(g => g.Id).ToArray();

            // 1. write cartridge.json
            var cartJson = JsonConvert.SerializeObject(new Cartridge()
            {
                cartridgeName = cartName
            }, Formatting.Indented);
            await File.WriteAllTextAsync($"{sdDrive}cartridge.json", cartJson);

            // 2. ensure the game directory exists on MicroSD
            Directory.CreateDirectory($"{sdDrive}game");

            // 3. Remove games on SD which are known but not selected
            //TODO: Remove games on SD which are known
            var sdGamesDir = new DirectoryInfo($"{sdDrive}game");
            foreach (var sdGameId in sdGamesDir.EnumerateFiles("*.json")
                .Select(j => Path.GetFileNameWithoutExtension(j.Name)))
            {
                if (_games.TryGetValue(sdGameId, out GameInfo foundGame) && !foundGame.IsSelected)
                {
                    DeleteGameFilesOnSD(sdGameId, sdDrive);
                }
            }

            int syncedGameIndex = 0;
            // 4. Save assets for each selected game
            foreach (var game in _games.Values.Where(g => g.IsSelected)) 
            {
                progress.Show(("Syncing Games", ++syncedGameIndex, selectedGameIds.Length));

                var platform = _appSettings.Platforms.Single(p => p.Id == game.romPlatformId);

                // 4a. copy emulator core (only overwrite if newer) + bios files (only overwrite if newer)
                //first select the right core (note: megadrive doesn't have a core)
                var selectedCore = game.RetroArchCore == null
                    ? platform.BlastRetroCore
                    : platform.RetroArchCores.FirstOrDefault(c => c.CoreFileName == game.RetroArchCore);

                if (selectedCore != null)
                {
                    foreach (var file in selectedCore.Files)
                    {
                        var sourceFile = new FileInfo(await _downloadManager.GetDownloadedFilePath(new Uri(file.SourceUrl), file.SourcePath));
                        var destFilePath = $"{sdDrive}{file.TargetPath}";
                        if (!File.Exists(destFilePath)) Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)); //ensure target dir exists
                        sourceFile.CopyToOverwriteIfNewer(destFilePath);
                    }

                    //copy over BIOS files (if not exists)
                    foreach (string biosFile in platform.BiosFiles)
                    {
                        var sourceBiosFile = new FileInfo($"{Constants.APP_ROOT_FOLDER}bios\\{platform.Alias}\\{biosFile}");
                        //bios files go into /sdcard/bios (for internal emulator) or /sdcard/retroarch/system (for RA cores)
                        var destBiosFilePath = $"{sdDrive}{(game.RetroArchCore == null ? "bios" : "retroarch\\system")}\\{biosFile}";
                        if (sourceBiosFile.Exists)
                        {
                            if (!File.Exists(destBiosFilePath)) Directory.CreateDirectory(Path.GetDirectoryName(destBiosFilePath)); //ensure target dir exists
                            sourceBiosFile.CopyToOverwriteIfNewer(destBiosFilePath);
                        }
                    }
                }

                // 4b. copy all image files (only overwrite if newer)
                var imagesDir = new DirectoryInfo($"{APP_GAMES_FOLDER}{game.Id}\\{SUBFOLDER_IMAGES}");
                if (imagesDir.Exists) imagesDir.GetFiles().ToList().ForEach(f =>
                {
                    f.CopyToOverwriteIfNewer($"{sdDrive}game\\{f.Name}");
                });

                // 4c. copy over rom file (only overwrite if newer)
                var targetRomDir = game.RetroArchCore != null
                        ? "roms"                                /* for RetroArch, put all roms under /sdcard/roms */
                        : (platform.Id == 1 ? "mame" : "game"); /* special case for mame roms, otherwise use /sdcard/game */
                Directory.CreateDirectory($"{sdDrive}{targetRomDir}"); //ensure target directory exists on MicroSD card
                var sourceRomDir = new DirectoryInfo($"{APP_GAMES_FOLDER}{game.Id}\\{SUBFOLDER_ROM}");
                if (sourceRomDir.Exists) sourceRomDir.GetFiles().ToList().ForEach(f =>
                {
                    var targetRomFileName = game.RetroArchCore != null
                        ? f.Name /* this allows multi-disks */
                        : game.romFileName;
                    var targetFile = $"{sdDrive}{targetRomDir}\\{targetRomFileName}";
                    f.CopyToOverwriteIfNewer(targetFile);
                });

                string multiDiscFilePath = null;
                if (game.IsMultiDisc)
                {
                    multiDiscFilePath = $"{sdDrive}{targetRomDir}\\{RemoveInvalidChars(game.romTitle)}.m3u";
                    File.WriteAllLines(multiDiscFilePath, sourceRomDir.GetFiles().Select(f => f.Name));
                }

                //custom handling for cores without autolaunch
                var gameJson = JsonConvert.SerializeObject(game, Formatting.Indented);
                var evercadeGameInfo = new EvercadeGameInfo(gameJson);
                if (selectedCore?.AutoLaunch == false)
                {
                    // for internal Arcade/MAME, we have special way of launching using .cue file
                    if (game.RetroArchCore == null && platform.Id == 1)
                    {
                        File.WriteAllText($"{sdDrive}game\\{game.Id}.cue", game.PreferedRomFileName());
                        evercadeGameInfo.romFileName = $"{game.Id}.cue";
                    }
                    else
                    {
                        //no auto launcher, so write out special script and 0-byte marker
                        evercadeGameInfo.romFileName = Path.GetFileNameWithoutExtension(evercadeGameInfo.romFileName);
                        File.Create($"{sdDrive}game\\{game.Id}").Dispose(); //0-byte marker

                        //create m3u for multi-disc games
                        //RemoveInvalidChars

                        Directory.CreateDirectory($"{sdDrive}special"); //ensure special directory exists

                        string shScript = game.RetroArchCore != null ? Resources.special_bash_ra : Resources.special_bash;
                        string scriptRomFileName = multiDiscFilePath != null ? Path.GetFileName(multiDiscFilePath) : game.PreferedRomFileName();
                        shScript = shScript
                            .Replace("{CORE_FILENAME}", selectedCore.CoreFileName)
                            .Replace("{ROM_FILENAME}", scriptRomFileName)
                            .Replace("\r", ""); //remove possible windows CR

                        await File.WriteAllTextAsync($"{sdDrive}special\\{game.Id}.sh", shScript, System.Text.Encoding.UTF8);
                    }
                }
                //now write out evercade game json
                var evercadeGameJson = JsonConvert.SerializeObject(evercadeGameInfo, Formatting.Indented);
                await File.WriteAllTextAsync($"{sdDrive}game\\{game.Id}.json", evercadeGameJson);
            }
        }

        private string GenerateGameId(string title)
        {
            //filter out stopwords
            var cleanTitle = string.Join(" ", title.Trim().ToLower()
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !(s.StartsWith("(") && s.EndsWith(")"))) //remove words in parentesis
                .Where(s => !s.In("the", "and", "a")));

            //filter out non-alphanumeric and maximum 16 chars
            cleanTitle = Regex.Replace(cleanTitle, "[^a-zA-Z0-9]", String.Empty);
            cleanTitle = cleanTitle.Substring(0, Math.Min(cleanTitle.Length, MAX_GAME_ID_LENGTH));

            int addedNumber = 2; //start with 2
            while (_games.ContainsKey(cleanTitle))
            {
                cleanTitle = cleanTitle.Substring(0, (cleanTitle+"_").IndexOf("_"));
                //replace last chars with a number
                cleanTitle = $"{cleanTitle}_{addedNumber++}";
                if (cleanTitle.Length > MAX_GAME_ID_LENGTH)
                {
                    cleanTitle = cleanTitle.Remove(cleanTitle.IndexOf("_")-1, 1);
                }
            }
            return cleanTitle;
        }

        /// <summary>
        /// Creates a new GameInfo and adds this to your local games database
        /// TODO: match CRC with local gamelist.xml and call out to TGB for game metadata
        /// </summary>
        /// <param name="romPath"></param>
        public async Task<IEnumerable<GameInfo>> ImportGamesByRom(string[] romPaths, IProgress<(string, int, int)> progress)
        {
            var newGames = new List<GameInfo>();
            int importedGames = 0;

            string multiDiskGameId = null;
            string multiDiskBaseTitle = null;

            foreach (var romPath in romPaths)
            {
                progress.Show(("Importing game(s)", ++importedGames, romPaths.Length));

                //1. don't add rom if CRC is already in collection
                (var romCRC32, var romMD5) = HashHelper.CalculateHashcodes(romPath);
                var crc32 = uint.Parse(romCRC32, NumberStyles.HexNumber);
                if (_gameCRCs.Contains(crc32)) continue;
                
                //2. calculate unique code
                var title = Path.GetFileNameWithoutExtension(romPath);
                var ext = Path.GetExtension(romPath).ToLower();
                var newId = GenerateGameId(title);
                var newRomFileName = $"{newId}{ext}";
                var originalRomFileName = $"{title}{ext}";

                //handle multi-disk
                if (Regex.IsMatch(title, @"\([Dd]isk [0-9]+ of [0-9]+\)"))
                {
                    var isDisk1 = title.ToLower().IndexOf("(disk 1 of ");
                    if (isDisk1 > 0)
                    {
                        multiDiskGameId = newId;
                        title = multiDiskBaseTitle = title.Substring(0, isDisk1); //update title so it won't display the "(disk 1 of ..."
                    }
                    else if (title.StartsWith(multiDiskBaseTitle))
                    {
                        File.Copy(romPath, $"{APP_GAMES_FOLDER}{multiDiskGameId}\\{SUBFOLDER_ROM}{originalRomFileName}", overwrite:true);
                        continue; //after copying the rom, we are done
                    }
                }
                else
                {
                    multiDiskGameId = multiDiskBaseTitle = null;
                }

                var platform = _appSettings.Platforms.OrderBy(p => p.BlastRetroCore?.AutoLaunch).FirstOrDefault(p => p.RomFileExtensions.Contains(ext));
                if (platform == null) continue; //!!!! unmapped extension. This should not be possible

                //create minimal GameInfo information
                var newGame = new GameInfo()
                {
                    Id = newId,
                    romTitle = title,
                    romFileName = platform.Id == 1 ? originalRomFileName : newRomFileName, //for MAME rom zips, don't change the filename
                    romPlatformId = platform.Id,
                    romCRC32 = romCRC32,
                    romMD5 = romMD5,
                    OriginalRomFileName = originalRomFileName,
                    IsRecentlyAdded = true,
                    IsMultiDisc = newId == multiDiskGameId
                };

                //if no internal core, preselect the first RA core
                if (platform.BlastRetroCore == null && platform.RetroArchCores.Length > 0)
                {
                    newGame.RetroArchCore = platform.RetroArchCores[0].CoreFileName;
                }

                //4. create required folders
                Directory.CreateDirectory($"{APP_GAMES_FOLDER}{newGame.Id}\\{SUBFOLDER_IMAGES_SOURCE}"); //this also creates the images subfolder
                Directory.CreateDirectory($"{APP_GAMES_FOLDER}{newGame.Id}\\{SUBFOLDER_ROM}");

                //4. copy over the rom file
                File.Copy(romPath, $"{APP_GAMES_FOLDER}{newGame.Id}\\{SUBFOLDER_ROM}{originalRomFileName}", overwrite: true);
                await SerializeGame(newGame);

                _games.Add(newId, newGame);
                _gameCRCs.Add(crc32);

                newGames.Add(newGame);
            }

            return newGames;
            //note: MainForm will take care of adding the new game to the UI
        }

        /// <summary>
        /// returns a list of relative filepaths used by game.
        /// these start with game\.., mame\.. or special\..
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="rootFolder"></param>
        /// <returns></returns>
        public void DeleteGameFilesOnSD(string gameId, string sdRootFolder)
        {
            //return if /sdcard/game/[gameId].json not exists
            if (!File.Exists($"{sdRootFolder}game\\{gameId}.json")) return;

            //if there is a mame cue file, get info from it and delete the rom from /sdcard/mame dir
            if (File.Exists($"{sdRootFolder}game\\{gameId}.cue"))
            {
                var mameFileName = File.ReadAllText($"{sdRootFolder}game\\{gameId}.cue");
                var mameFilePath = $"{sdRootFolder}mame\\{mameFileName}";
                if (File.Exists(mameFilePath)) File.Delete(mameFilePath);
            }

            //TODO: figure out how to remove rom in /roms folder

            //remove special script
            var specialScriptPath = $"{sdRootFolder}special\\{gameId}.sh";
            if (File.Exists(specialScriptPath)) File.Delete(specialScriptPath);

            var sdGameDir = new DirectoryInfo($"{sdRootFolder}game");
            sdGameDir.EnumerateFiles($"{gameId}.*") //json, rom, and possiblye cue file or 0-byte special marker
                .Concat(sdGameDir.EnumerateFiles($"{gameId}0*.png")) //artwork gfx
                .Concat(sdGameDir.EnumerateFiles($"{gameId}1*.png")) //artwork gfx
                .Concat(sdGameDir.EnumerateFiles($"{gameId}2*.png")) //artwork gfx
                .Concat(sdGameDir.EnumerateFiles($"{gameId}_gamebanner.png")) //optional banner gfx
                .ToList().ForEach(f => File.Delete(f.FullName));
        }

        public void DeleteGameByIds(IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                Directory.Delete($"{APP_GAMES_FOLDER}{id}", recursive:true);

                //now delete from memory and CRC dictionary
                if (_games.Remove(id, out GameInfo game) && uint.TryParse(game.romCRC32, NumberStyles.HexNumber, null, out uint crc32))
                {
                    _gameCRCs.Remove(crc32);
                }
            }
        }

        public static string GetSourceImagePath(string imagePath)
        {
            return Path.Combine(Path.GetDirectoryName(imagePath), "source", Path.GetFileName(imagePath));
        }

        /// <summary>
        /// Reads games from app games filder
        /// </summary>
        public void ReadGames(IProgress<(string, int, int)> progress)
        {
            //reads games from database folder
            if (!Directory.Exists(APP_GAMES_FOLDER)) return;

            var allGameDirs = new DirectoryInfo(APP_GAMES_FOLDER).GetDirectories();
            var totalGameDirs = allGameDirs.Length;
            var loadedGamesCount = 0;

            foreach (var gameDir in allGameDirs) 
            {
                progress.Show(("Loading games", ++loadedGamesCount, totalGameDirs));

                var jsonFilePath = $"{gameDir.FullName}\\{gameDir.Name}.json";
                if (!File.Exists(jsonFilePath)) continue;

                try
                {
                    var gameInfo = JsonConvert.DeserializeObject<GameInfo>(File.ReadAllText(jsonFilePath));
                    if (gameInfo.Id != gameDir.Name) continue; //skip 

                    //rom MUST exist
                    var romsDir = new DirectoryInfo($"{APP_GAMES_FOLDER}{gameInfo.Id}\\{SUBFOLDER_ROM}");
                    if (!romsDir.Exists || romsDir.GetFiles().Length == 0) continue; //skip 

                    //TODO: before skipping invalid game folder, maybe clean it up first

                    //ensure image folders exist
                    Directory.CreateDirectory($"{APP_GAMES_FOLDER}{gameInfo.Id}\\{SUBFOLDER_IMAGES_SOURCE}"); //this also creates the images subfolder

                    _games.Add(gameInfo.Id, gameInfo);
                    _gameCRCs.Add(uint.Parse(gameInfo.romCRC32, NumberStyles.HexNumber));
                }
                catch
                {
                    //TODO:log
                }
            }
        }

        internal async Task EnrichGames(IEnumerable<string> gameIds, IProgress<(string, int, int)> progress)
        {
            await EnrichGames(gameIds.Select(i => GetGameById(i)).Where(g => g != null), progress);
        }

        internal string GetCleanedTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            //strip everything after brackets
            int bracketsIndex = title.IndexOfAny(new[] { '(', '[' });
            if (bracketsIndex > 1) title = title.Substring(0, bracketsIndex);

            //...and trim
            return title.Trim();
        }

        internal string GetCompareTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            title = GetCleanedTitle(title).ToLowerInvariant();

            //only keep words
            title = string.Join(" ", Regex.Replace(title, "[^A-Za-z0-9]", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

            title = title.Replace(" & ", " and ");
            title = title.Replace(" s ", " "); //probably this was a 's

            return title;
        }

        internal async Task EnrichGames(IEnumerable<GameInfo> games, IProgress<(string, int, int)> progress)
        {
            var mappedGames = new Dictionary<int, List<GameInfo>>();
            int mappedGamesCount = 0;

            void AddGameToMapped(GameInfo game, int tgdbId)
            {
                mappedGamesCount++;
                game.TgdbId = tgdbId;
                if (mappedGames.ContainsKey(tgdbId))
                {
                    mappedGames[tgdbId].Add(game);
                }
                else
                {
                    mappedGames.Add(tgdbId, new List<GameInfo>() { game });
                }
            }
            
            //build list of mappedGames
            foreach (var game in games)
            {
                if (_romManager.RomMappings.TryGetValue(uint.Parse(game.romCRC32, NumberStyles.HexNumber), out int tgdbId))
                {
                    AddGameToMapped(game, tgdbId);
                }
            }

            int processedGames = 0;

            if (mappedGamesCount > 0)
            {
                var resp = await _tgdbApi.Games.ByGameID(
                    mappedGames.Keys.ToArray(),
                    new[] { GameFieldIncludes.BoxArt },
                    GameFields.Players, GameFields.Publishers, GameFields.Genres, GameFields.Overview, GameFields.Platform);
                do
                {
                    if (resp.Code == 200)
                    {
                        var imgBaseUrl = resp.Include?.BoxArt?.BaseUrl?.Medium;
                        foreach (var tgdbGame in resp.Data.Games)
                        {
                            foreach (var mappedGame in mappedGames[tgdbGame.Id])
                            {
                                progress.Show(("Scraping Game info by CRC code", ++processedGames, mappedGamesCount));
                                //metadata
                                mappedGame.romDescription = tgdbGame.Overview;
                                mappedGame.romPlayers = tgdbGame.Players.HasValue ? tgdbGame.Players.Value : 1; //default 1
                                mappedGame.romReleaseDate = tgdbGame.ReleaseDate.HasValue ? tgdbGame.ReleaseDate.Value.ToString("yyyy-MM-dd") : "";
                                mappedGame.romPlatformId = _romManager.TryMapToPlatform(tgdbGame.Platform, out int platform) ? platform : mappedGame.romPlatformId;
                                mappedGame.romTitle = !string.IsNullOrWhiteSpace(tgdbGame.GameTitle) ? tgdbGame.GameTitle : mappedGame.romTitle;
                                mappedGame.romGenre = _romManager.MapToGenre(tgdbGame.Genres);

                                //images
                                if (resp.Include?.BoxArt?.Data?.ContainsKey(tgdbGame.Id) == true)
                                {
                                    var selectedImg = resp.Include.BoxArt.Data[tgdbGame.Id].OrderBy(i => i.Side == "front" ? 1 : 2).FirstOrDefault();
                                    if (selectedImg != null)
                                    {
                                        await _imageManager.ResizeImage($"{imgBaseUrl}{selectedImg.FileName}", mappedGame, GetGameBoxartImageInfo(mappedGame.Id));
                                    }
                                }
                            }
                        }
                    }
                    //go to next page
                    resp = resp.Pages?.Next != null ? await resp.NextPage() : null;
                }
                while (resp != null);
            }

            //now for the unmapped games, try to find a single match (by name)
            //if found => add to mapped
            var nonMappedGames = games.Except(mappedGames.Values.SelectMany(g => g));
            var nonMappedGamesCount = nonMappedGames.Count();
            processedGames = 0;
            foreach (var nonMappedGame in nonMappedGames)
            {
                var tgdbPlatformIds = GetGamePlatformsByRomExtesion(Path.GetExtension(nonMappedGame.romFileName))
                    .SelectMany(p => p.TGDB_PlatformIds)
                    .Select(p => p.Id).ToArray();
                //
                var resp = await _tgdbApi.Games.ByGameName(GetCleanedTitle(nonMappedGame.romTitle), 1, tgdbPlatformIds
                    , new[] { GameFieldIncludes.BoxArt },
                    GameFields.Players, GameFields.Publishers, GameFields.Genres, GameFields.Overview, GameFields.Platform);

                progress.Show(("Scraping Game info by name", ++processedGames, nonMappedGamesCount));

                if (resp.Code == 200)
                {
                    var imgBaseUrl = resp.Include?.BoxArt?.BaseUrl?.Medium;

                    var tgdbGames = resp.Data.Games.Where(g => GetCompareTitle(g.GameTitle) == GetCompareTitle(nonMappedGame.romTitle));
                    var firstMatchPlatform = tgdbGames.FirstOrDefault()?.Platform;
                    // if matches are for the same platform, then we can be pretty sure the first match is a good one
                    if (tgdbGames.Count() > 0 && tgdbGames.All(g => g.Platform == firstMatchPlatform))
                    {
                        var tgdbGame = tgdbGames.First();
                        var mappedGame = nonMappedGame;

                        AddGameToMapped(mappedGame, tgdbGame.Id);

                        //TODO: extract code below to method

                        //metadata
                        mappedGame.romDescription = tgdbGame.Overview;
                        mappedGame.romPlayers = tgdbGame.Players.HasValue ? tgdbGame.Players.Value : 1; //default 1
                        mappedGame.romReleaseDate = tgdbGame.ReleaseDate.HasValue ? tgdbGame.ReleaseDate.Value.ToString("yyyy-MM-dd") : "";
                        mappedGame.romPlatformId = _romManager.TryMapToPlatform(tgdbGame.Platform, out int platform) ? platform : mappedGame.romPlatformId;
                        //probably don't overwrite title, as we want to keep original name in case match was a false positive
                        //mappedGame.romTitle = !string.IsNullOrWhiteSpace(tgdbGame.GameTitle) ? tgdbGame.GameTitle : mappedGame.romTitle;
                        mappedGame.romGenre = _romManager.MapToGenre(tgdbGame.Genres);

                        //images
                        if (resp.Include?.BoxArt?.Data?.ContainsKey(tgdbGame.Id) == true)
                        {
                            var selectedImg = resp.Include.BoxArt.Data[tgdbGame.Id].OrderBy(i => i.Side == "front" ? 1 : 2).FirstOrDefault();
                            if (selectedImg != null)
                            {
                                await _imageManager.ResizeImage($"{imgBaseUrl}{selectedImg.FileName}", mappedGame, GetGameBoxartImageInfo(mappedGame.Id));
                            }
                        }
                    }
                }
            }

            if (mappedGamesCount > 0)
            {
                //now try to find a nice banner
                var respImg = await _tgdbApi.Games.Images(
                    mappedGames.Keys.ToArray(),
                    GameImageType.Screenshot, GameImageType.TitleScreen, GameImageType.Fanart);
                processedGames = 0;
                do
                {
                    if (respImg.Code == 200)
                    {
                        var imgBaseUrl = respImg.Data.BaseUrl.Medium;
                        foreach (var tgdbImg in respImg.Data.Images)
                        {
                            foreach (var mappedGame in mappedGames[tgdbImg.Key])
                            {
                                progress.Show(("Looking for banner images", ++processedGames, mappedGamesCount));
                                var selectedImg = tgdbImg.Value.OrderBy(i => i.Type == GameImageType.Screenshot ? 0 : 1).FirstOrDefault(); //first screenshot, then titlescreen and fanart
                                if (selectedImg != null)
                                {
                                    await _imageManager.ResizeImage($"{imgBaseUrl}{selectedImg.FileName}", mappedGame, new[] { GetGameImageInfo(mappedGame.Id, ImageType.Banner) });
                                }
                            }
                        }
                    }
                    //go to next page
                    respImg = respImg.Pages?.Next != null ? await respImg.NextPage() : null;
                }
                while (respImg != null);
            }

            //now serialize the games
            processedGames = 0;
            foreach (var gameInfo in mappedGames.Values.SelectMany(g => g))
            {
                progress.Show(("Updating game database", ++processedGames, mappedGamesCount));
                await SerializeGame(gameInfo);
            }
        }

        public async Task ClearImage(GameInfo gameInfo, ImageType imageType, bool autoSerialize = true)
        {
            var imagePath = GetGameImageInfo(gameInfo.Id, imageType).LocalPath;
            File.Delete(imagePath);

            //also remove source image
            var sourceImg = GetSourceImagePath(imagePath);
            if (File.Exists(sourceImg)) File.Delete(sourceImg);

            switch (imageType)
            {
                case ImageType.Small: gameInfo.Image = null; break;
                case ImageType.Medium: gameInfo.ImageHD = null; break;
                case ImageType.Large: gameInfo.Image1080 = null; break;
                case ImageType.Banner: gameInfo.ImageBanner = null; break;
            }

            if (autoSerialize) await SerializeGame(gameInfo);
        }

        public string GetRomListTitle(GameInfo game)
        {
            if (game == null) return "";
            return $"{game.romTitle} [{game.romPlatform}]";
        }
    }
}
