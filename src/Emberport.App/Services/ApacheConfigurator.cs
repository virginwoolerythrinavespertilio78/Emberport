using System.Collections.Generic;
using System.IO;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Produces an Emberport-owned httpd.conf so the shipped file stays untouched.</summary>
public static class ApacheConfigurator
{
    public const int DefaultPort = 80;

    public static string Prepare(BinaryInstallation apache, BinaryInstallation? php, int port)
    {
        var templatePath = Path.Combine(apache.DirectoryPath, "conf", "httpd.conf");

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Apache is missing its default configuration.", templatePath);
        }

        var serverRoot = ToApachePath(apache.DirectoryPath);
        var documentRoot = EnsureDocumentRoot();

        var rewritten = new List<string>();

        foreach (var line in File.ReadAllLines(templatePath))
        {
            rewritten.Add(Rewrite(line, serverRoot, documentRoot, port));
        }

        // Apache honours the last matching directive, so appending here always wins
        // regardless of how the shipped configuration is formatted.
        rewritten.Add(string.Empty);
        rewritten.Add("# --- Emberport ---------------------------------------------------------");
        rewritten.Add($"DocumentRoot \"{documentRoot}\"");
        rewritten.Add($"<Directory \"{documentRoot}\">");
        rewritten.Add("    Options Indexes FollowSymLinks");
        rewritten.Add("    AllowOverride All");
        rewritten.Add("    Require all granted");
        rewritten.Add("</Directory>");
        rewritten.Add("DirectoryIndex index.php index.html index.htm");

        AppendPhp(rewritten, php);
        AppendPhpMyAdmin(rewritten);

        var outputPath = Path.Combine(apache.DirectoryPath, "conf", "emberport.conf");
        File.WriteAllLines(outputPath, rewritten);

        return outputPath;
    }

    private static string Rewrite(string line, string serverRoot, string documentRoot, int port)
    {
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("Define SRVROOT", StringComparison.OrdinalIgnoreCase))
        {
            return $"Define SRVROOT \"{serverRoot}\"";
        }

        if (trimmed.StartsWith("ServerRoot", StringComparison.OrdinalIgnoreCase))
        {
            return $"ServerRoot \"{serverRoot}\"";
        }

        if (trimmed.StartsWith("Listen", StringComparison.OrdinalIgnoreCase))
        {
            return $"Listen {port}";
        }

        // Covers both the active directive and the commented sample line.
        if (trimmed.StartsWith("ServerName", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("#ServerName", StringComparison.OrdinalIgnoreCase))
        {
            return $"ServerName localhost:{port}";
        }

        if (trimmed.StartsWith("DocumentRoot", StringComparison.OrdinalIgnoreCase))
        {
            return $"DocumentRoot \"{documentRoot}\"";
        }

        if (trimmed.StartsWith("<Directory \"${SRVROOT}/htdocs\">", StringComparison.OrdinalIgnoreCase))
        {
            return $"<Directory \"{documentRoot}\">";
        }

        return line;
    }

    private static string EnsureDocumentRoot()
    {
        Directory.CreateDirectory(AppPaths.WwwRoot);

        var landingPage = Path.Combine(AppPaths.WwwRoot, "index.html");

        if (!File.Exists(landingPage))
        {
            File.WriteAllText(landingPage, LandingPage);
        }

        return ToApachePath(AppPaths.WwwRoot);
    }

    private static void AppendPhp(List<string> lines, BinaryInstallation? php)
    {
        if (php is null)
        {
            return;
        }

        var module = PhpConfigurator.FindApacheModule(php);

        if (module is null)
        {
            // A non thread safe build has no Apache module; it can only run as CGI.
            return;
        }

        lines.Add(string.Empty);
        lines.Add($"# PHP {php.Version}");
        lines.Add($"LoadModule php_module \"{ToApachePath(module)}\"");
        lines.Add("AddHandler application/x-httpd-php .php");
        lines.Add($"PHPIniDir \"{ToApachePath(php.DirectoryPath)}\"");
    }

    private static void AppendPhpMyAdmin(List<string> lines)
    {
        var path = Path.Combine(AppPaths.ToolsRoot, "phpmyadmin");

        if (!Directory.Exists(path))
        {
            return;
        }

        var alias = ToApachePath(path);

        lines.Add(string.Empty);
        lines.Add("# phpMyAdmin");
        lines.Add($"Alias /phpmyadmin \"{alias}\"");
        lines.Add($"<Directory \"{alias}\">");
        lines.Add("    Options Indexes FollowSymLinks");
        lines.Add("    AllowOverride All");
        lines.Add("    Require local");
        lines.Add("</Directory>");
    }

    // Apache only understands forward slashes, even on Windows.
    private static string ToApachePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    private const string LandingPage = """
    <!doctype html>
    <html lang="en">
    <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Emberport — Local Development Environment</title>
    <style>
        * { box-sizing: border-box; }
        :root {
            --ember: #FF6B1A;
            --ember-soft: #FF8340;
            --deep: #C2410C;
            --bg: #0E0E10;
            --muted: #A1A1AA;
        }
        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            overflow: hidden;
            position: relative;
            background: var(--bg);
            color: #F2F2F5;
            font-family: "Segoe UI Variable Display", "Segoe UI", sans-serif;
        }
        .grid {
            position: fixed;
            inset: 0;
            background-image:
                linear-gradient(rgba(255,255,255,.035) 1px, transparent 1px),
                linear-gradient(90deg, rgba(255,255,255,.035) 1px, transparent 1px);
            background-size: 64px 64px;
            -webkit-mask-image: radial-gradient(circle at 50% 42%, #000, transparent 72%);
            mask-image: radial-gradient(circle at 50% 42%, #000, transparent 72%);
        }
        .glow {
            position: fixed;
            border-radius: 50%;
            filter: blur(130px);
            opacity: .45;
            animation: drift 16s ease-in-out infinite alternate;
        }
        .glow.a { width: 540px; height: 540px; background: var(--ember); top: -200px; left: 26%; }
        .glow.b { width: 460px; height: 460px; background: var(--deep); bottom: -220px; right: -60px; animation-delay: -8s; }
        @keyframes drift { to { transform: translate(-50px, 40px) scale(1.18); } }
        main { position: relative; text-align: center; padding: 40px; }
        .badge {
            display: inline-flex;
            align-items: center;
            gap: 9px;
            padding: 7px 15px;
            border: 1px solid rgba(255,255,255,.12);
            border-radius: 999px;
            background: rgba(255,255,255,.04);
            backdrop-filter: blur(14px);
            font-size: 11px;
            letter-spacing: .16em;
            text-transform: uppercase;
            color: var(--muted);
        }
        .dot {
            width: 7px;
            height: 7px;
            border-radius: 50%;
            background: #3DD68C;
            box-shadow: 0 0 14px #3DD68C;
            animation: pulse 2.2s ease-in-out infinite;
        }
        @keyframes pulse { 50% { opacity: .3; } }
        h1 {
            margin: 28px 0 0;
            font-size: clamp(52px, 9vw, 108px);
            font-weight: 700;
            line-height: 1;
            letter-spacing: -.055em;
            background: linear-gradient(180deg, #FFFFFF 28%, #FFAE7A 72%, var(--ember));
            -webkit-background-clip: text;
            background-clip: text;
            color: transparent;
        }
        .tagline {
            margin-top: 20px;
            font-size: 12px;
            letter-spacing: .46em;
            text-transform: uppercase;
            color: var(--ember-soft);
        }
        .lead {
            margin: 28px auto 0;
            max-width: 520px;
            font-size: 15px;
            line-height: 1.75;
            color: var(--muted);
        }
        code {
            padding: 2px 7px;
            border-radius: 6px;
            background: rgba(255,107,26,.1);
            font-family: "Cascadia Mono", Consolas, monospace;
            font-size: 13px;
            color: var(--ember-soft);
        }
        .stack { margin-top: 40px; display: flex; flex-wrap: wrap; gap: 10px; justify-content: center; }
        .chip {
            padding: 10px 18px;
            border: 1px solid rgba(255,255,255,.09);
            border-radius: 11px;
            background: rgba(255,255,255,.03);
            backdrop-filter: blur(10px);
            font-size: 13px;
            color: #D4D4D8;
            transition: transform .25s, border-color .25s, color .25s;
        }
        .chip:hover { transform: translateY(-3px); border-color: rgba(255,107,26,.55); color: #FFF; }
        footer {
            position: fixed;
            left: 0;
            right: 0;
            bottom: 28px;
            text-align: center;
            font-size: 12px;
            color: #6E6E78;
        }
        footer a { color: #A1A1AA; text-decoration: none; border-bottom: 1px solid rgba(255,255,255,.16); }
        footer a:hover { color: var(--ember); }
    </style>
    </head>
    <body>
        <div class="grid"></div>
        <div class="glow a"></div>
        <div class="glow b"></div>

        <main>
            <div class="badge"><span class="dot"></span> Server Online</div>
            <h1>Emberport</h1>
            <div class="tagline">Deploy &middot; Manage &middot; Ignite</div>
            <p class="lead">
                Welcome aboard. Your local development environment is live.
                Drop a project into the <code>www</code> folder and it is served instantly.
            </p>
            <div class="stack">
                <div class="chip">Apache</div>
                <div class="chip">PHP</div>
                <div class="chip">MySQL</div>
                <div class="chip">Redis</div>
                <div class="chip">phpMyAdmin</div>
            </div>
        </main>

        <footer>
            Crafted by <a href="https://github.com/hojjatjh" target="_blank" rel="noopener">Hojjat Jahanpour</a>
            &nbsp;&middot;&nbsp; star it on GitHub &#9733;
        </footer>
    </body>
    </html>
    """;
}