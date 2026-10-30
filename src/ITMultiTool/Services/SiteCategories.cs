namespace ITMultiTool.Services;

public sealed record SiteCategory(string Name, string Note, IReadOnlyList<string> Domains)
{
    public override string ToString() => $"{Name} ({Domains.Count} sites)";
}

/// <summary>
/// Ready-made lists of non-work websites. The hosts file has no wildcards, so common
/// subdomains and short links are listed separately ("www." is added automatically).
/// </summary>
public static class SiteCategories
{
    public static IReadOnlyList<SiteCategory> All { get; } =
    [
        new("Social media", "",
        [
            "facebook.com", "m.facebook.com", "web.facebook.com", "fb.com", "fb.watch",
            "instagram.com", "tiktok.com", "vt.tiktok.com",
            "x.com", "twitter.com", "t.co",
            "threads.net", "threads.com", "snapchat.com", "pinterest.com",
            "reddit.com", "old.reddit.com", "redd.it", "tumblr.com",
        ]),
        new("Video streaming", "Some staff may use YouTube for training videos.",
        [
            "youtube.com", "m.youtube.com", "youtu.be", "youtube-nocookie.com",
            "netflix.com", "primevideo.com", "disneyplus.com", "hulu.com", "max.com", "hbomax.com",
            "twitch.tv", "kick.com", "dailymotion.com", "bilibili.com", "bilibili.tv",
            "iq.com", "viu.com", "crunchyroll.com", "9anime.to",
        ]),
        new("Music streaming", "",
        [
            "spotify.com", "open.spotify.com", "music.youtube.com", "soundcloud.com",
            "deezer.com", "music.apple.com", "tidal.com",
        ]),
        new("Online games", "",
        [
            "roblox.com", "steampowered.com", "store.steampowered.com", "steamcommunity.com",
            "epicgames.com", "riotgames.com", "leagueoflegends.com", "garena.com", "mobilelegends.com",
            "miniclip.com", "poki.com", "crazygames.com", "y8.com", "friv.com",
            "chess.com", "lichess.org", "addictinggames.com",
        ]),
        new("Online shopping", "Purchasing staff may need some of these.",
        [
            "shopee.ph", "lazada.com.ph", "zalora.com.ph", "temu.com", "shein.com",
            "aliexpress.com", "amazon.com", "ebay.com", "etsy.com", "wish.com",
        ]),
        new("Gambling and betting", "",
        [
            "bet365.com", "1xbet.com", "stake.com", "pokerstars.com", "888casino.com",
            "draftkings.com", "fanduel.com", "betway.com", "williamhill.com", "okbet.com",
        ]),
        new("Dating", "",
        [
            "tinder.com", "bumble.com", "okcupid.com", "match.com", "hinge.co",
            "badoo.com", "pof.com", "tagged.com",
        ]),
        new("Entertainment and memes", "",
        [
            "9gag.com", "imgur.com", "buzzfeed.com", "boredpanda.com", "knowyourmeme.com",
            "ifunny.co", "cracked.com", "wattpad.com", "webtoons.com",
        ]),
        new("Personal messaging", "Check first: many teams use these for work.",
        [
            "messenger.com", "discord.com", "discord.gg", "discordapp.com",
            "web.whatsapp.com", "telegram.org", "web.telegram.org", "viber.com",
        ]),
    ];
}
