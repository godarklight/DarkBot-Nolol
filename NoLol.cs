using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using DarkBot;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace DarkBot_NoLol
{
    public class NoLol : BotModule
    {
        private long maxTime = TimeSpan.TicksPerMinute * 30;
        Dictionary<ulong, long> userStartTime = new Dictionary<ulong, long>();
        Dictionary<ulong, long> shameList = new Dictionary<ulong, long>();
        private DiscordSocketClient _client = null;
        private Task looper;
        Random rand = new Random();

        public Task Initialize(IServiceProvider services)
        {
            LoadDatabase();
            _client = services.GetService(typeof(DiscordSocketClient)) as DiscordSocketClient;
            _client.Ready += OnReady;
            _client.PresenceUpdated += PresenceUpdated;
            return Task.CompletedTask;
        }

        private async Task OnReady()
        {
            Log(LogSeverity.Info, "NoLol ready!");
            looper = new Task(Looper);
            looper.Start();
        }

        private Task PresenceUpdated(SocketUser user, SocketPresence oldPresence, SocketPresence newPresence)
        {
            bool playingLoL = false;
            foreach (IActivity activity in newPresence.Activities)
            {
                RichGame game = activity as RichGame;
                if (game != null && game.Name == "League of Legends")
                {
                    playingLoL = true;

                }
            }
            if (!playingLoL)
            {
                if (userStartTime.ContainsKey(user.Id))
                {
                    userStartTime.Remove(user.Id);
                    Log(LogSeverity.Info, $"{user.Id} = {user.Username}#{user.Discriminator} is quit LoL");
                }
            }
            if (playingLoL && !shameList.ContainsKey(user.Id))
            {
                if (!userStartTime.ContainsKey(user.Id))
                {
                    userStartTime[user.Id] = DateTime.UtcNow.Ticks;
                    Log(LogSeverity.Info, $"{user.Id} = {user.Username}#{user.Discriminator} is playing LoL");
                }
            }
            return Task.CompletedTask;
        }

        private async void Looper()
        {
            List<ulong> removeList = new List<ulong>();
            while (true)
            {
                foreach (KeyValuePair<ulong, long> user in userStartTime)
                {
                    long elapsedTime = DateTime.UtcNow.Ticks - user.Value;
                    if (elapsedTime > maxTime)
                    {
                        removeList.Add(user.Key);
                        await ShameUser(user.Key);
                    }
                    else
                    {
                        long timeLeft = 30 - (elapsedTime / TimeSpan.TicksPerMinute);
                        Log(LogSeverity.Info, $"{timeLeft} minutes left to shame {user.Key}");
                    }
                }
                if (removeList.Count > 0)
                {
                    foreach (ulong removeID in removeList)
                    {
                        userStartTime.Remove(removeID);
                    }
                    removeList.Clear();
                }
                await Task.Delay(60000);
            }
        }

        private async Task ShameUser(ulong user)
        {
            if (shameList.ContainsKey(user))
            {
                //Only annoy once
                Log(LogSeverity.Info, $"{user} has already been shamed");
                return;
            }
            shameList.Add(user, DateTime.UtcNow.Ticks);
            SaveDatabase();
            foreach (SocketGuild sg in _client.Guilds)
            {
                SocketGuildUser sgu = sg.GetUser(user);
                if (sgu == null)
                {
                    continue;
                }
                SocketTextChannel selectedChannel = null;
                SocketTextChannel selectedChannelBackup = null;
                foreach (SocketTextChannel textChannel in sg.TextChannels)
                {
                    //Find general
                    if (textChannel.Name == "general")
                    {
                        selectedChannel = textChannel;
                    }
                    //Find a general like channel
                    if (textChannel.Name.Contains("general"))
                    {
                        if (selectedChannelBackup == null)
                        {
                            selectedChannelBackup = textChannel;
                        }
                        else
                        {
                            //Select the shortest name channel if more than 1 is named general
                            if (selectedChannelBackup.Name.Length > textChannel.Name.Length)
                            {
                                selectedChannelBackup = textChannel;
                            }
                        }
                    }
                }
                if (selectedChannel != null)
                {
                    if (selectedChannel.GetUser(user) != null)
                    {
                        Log(LogSeverity.Info, $"<@{user}> has been added to the LoL hall of shame for playing more than half an hour!");
                        await selectedChannel.SendMessageAsync($"<@{user}> has been added to the LoL hall of shame for playing more than half an hour!");
                        return;
                    }
                }
                if (selectedChannelBackup != null)
                {
                    if (selectedChannelBackup.GetUser(user) != null)
                    {
                        Log(LogSeverity.Info, $"<@{user}> has been added to the LoL hall of shame for playing more than half an hour!");
                        await selectedChannelBackup.SendMessageAsync($"<@{user}> has been added to the LoL hall of shame for playing more than half an hour!");
                    }
                }
            }
        }

        private async Task Say(SocketTextChannel stc, string message)
        {
            await stc.SendMessageAsync(message);
        }

        private void Log(LogSeverity severity, string text)
        {
            LogMessage logMessage = new LogMessage(severity, "NoLol", text);
            Program.LogAsync(logMessage);
        }

        private void LoadDatabase()
        {
            shameList.Clear();
            string databaseString = DataStore.Load("NoLol");
            if (databaseString == null)
            {
                return;
            }
            using (StringReader sr = new StringReader(databaseString))
            {
                string currentLine = null;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    int index = currentLine.IndexOf("=");
                    string lhs = currentLine.Substring(0, index);
                    string rhs = currentLine.Substring(index + 1);
                    if (ulong.TryParse(lhs, out ulong lhsParse))
                    {
                        if (long.TryParse(rhs, out long rhsParse))
                        {
                            shameList.Add(lhsParse, rhsParse);
                        }
                    }
                }
            }
        }

        private void SaveDatabase()
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<ulong, long> shame in shameList)
            {
                sb.AppendLine($"{shame.Key}={shame.Value}");
            }
            DataStore.Save("NoLol", sb.ToString());
        }
    }
}