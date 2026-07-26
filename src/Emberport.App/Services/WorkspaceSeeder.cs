using System;
using System.IO;

namespace Emberport.Services;

/// <summary>
/// Writes the sample files once, and only into the folder Emberport owns.
/// A custom server root belongs to the user and is never touched.
/// </summary>
public static class WorkspaceSeeder
{
    private const string IndexFileName = "index.html";
    private const string InfoFileName = "info.php";

    public static void Seed()
    {
        var root = AppPaths.DefaultWwwRoot;

        try
        {
            Directory.CreateDirectory(root);

            WriteIfAbsent(Path.Combine(root, IndexFileName), Welcome());
            WriteIfAbsent(Path.Combine(root, InfoFileName), "<?php\n\nphpinfo();\n");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Seeding is a convenience, never a reason to block startup.
        }
    }

    // Deleting a seeded file is a decision, so it is not undone on the next run.
    private static void WriteIfAbsent(string path, string content)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllText(path, content);
    }

    private static string Welcome() =>
        """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Emberport</title>
            <style>
                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background: #0e0e10;
                    color: #f2f2f5;
                    font-family: "Segoe UI", system-ui, sans-serif;
                }
                .card {
                    width: 560px;
                    max-width: 90vw;
                    padding: 40px;
                    border-radius: 14px;
                    background: #16161a;
                    border: 1px solid #232329;
                }
                .mark {
                    width: 46px;
                    height: 46px;
                    border-radius: 10px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background: rgba(255, 107, 26, 0.08);
                    border: 1px solid rgba(255, 107, 26, 0.18);
                    color: #ff8340;
                    font-size: 22px;
                    font-weight: 700;
                }
                h1 { margin: 22px 0 0; font-size: 27px; letter-spacing: -0.4px; }
                p { margin: 12px 0 0; color: #a1a1aa; line-height: 1.65; font-size: 14px; }
                .tag {
                    margin-top: 26px;
                    font-size: 11px;
                    letter-spacing: 2px;
                    color: #6e6e78;
                }
                code {
                    padding: 2px 6px;
                    border-radius: 4px;
                    background: #0b0b0d;
                    border: 1px solid #232329;
                    color: #ff8340;
                    font-size: 13px;
                }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="mark">E</div>
                <h1>Emberport is running.</h1>
                <p>
                    Apache is serving this folder. Drop a project in here, or create one from the
                    Sites page, and it becomes available at <code>/project-name/</code>.
                </p>
                <p>Check the interpreter with <code>info.php</code>.</p>
                <div class="tag">DEPLOY. MANAGE. IGNITE.</div>
            </div>
        </body>
        </html>
        """;
}