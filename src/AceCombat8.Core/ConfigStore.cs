using System.Security.Cryptography;
using static AceCombat8.Core.Lang;

namespace AceCombat8.Core;

public sealed record SaveResult(bool Changed, string? BackupPath, FlightStickDocument Document);
public sealed record BackupEntry(string Path, DateTime Created, long Size)
{
    public string Label => $"{Created:yyyy-MM-dd HH:mm:ss}   ·   {System.IO.Path.GetFileName(Path)}";
}

public static class ConfigStore
{
    public static SaveResult Save(EditSession session, Func<bool>? gameRunning = null)
    {
        if (gameRunning?.Invoke() == true)
            throw new InvalidOperationException(T("请先退出游戏再保存。", "Exit the game before saving.", "保存する前にゲームを終了してください。"));
        var document = session.Document;
        byte[] output = document.Build(session.Current);
        if (Hash(File.ReadAllBytes(document.Path)) != document.Hash)
            throw new InvalidOperationException(ChangedFileMessage());
        if (output.AsSpan().SequenceEqual(document.OriginalBytes)) return new(false, null, document);
        // Validate the complete proposed document before touching the original file.
        _ = FlightStickDocument.FromBytes(document.Path, output, document.Device);
        string directory = System.IO.Path.GetDirectoryName(document.Path)!;
        string stem = System.IO.Path.GetFileNameWithoutExtension(document.Path);
        string suffix = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
        string backup = System.IO.Path.Combine(directory, $"{stem}.before-pxn-tool-{suffix}.ini.bak");
        string temp = System.IO.Path.Combine(directory, $".{stem}.{suffix}.tmp");
        File.Copy(document.Path, backup, false);
        if (Hash(File.ReadAllBytes(backup)) != document.Hash)
            throw new InvalidOperationException(ChangedFileMessage());
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(output);
                stream.Flush(true);
            }
            if (Hash(File.ReadAllBytes(document.Path)) != document.Hash)
                throw new InvalidOperationException(ChangedFileMessage());
            File.Replace(temp, document.Path, null);
            if (!File.ReadAllBytes(document.Path).AsSpan().SequenceEqual(output))
                throw new IOException(T("保存校验失败。备份位于 ", "The saved file could not be verified. Backup at ", "保存結果を検証できませんでした。バックアップ先 ") + backup);
            return new(true, backup, FlightStickDocument.Load(document.Path, document.Device));
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static IReadOnlyList<BackupEntry> ListBackups(string configPath)
    {
        string directory = System.IO.Path.GetDirectoryName(configPath)!;
        string stem = System.IO.Path.GetFileNameWithoutExtension(configPath);
        return Directory.EnumerateFiles(directory, stem + "*.ini.bak", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p)).OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupEntry(f.FullName, f.LastWriteTime, f.Length)).ToList();
    }

    public static EditSession PrepareRestore(FlightStickDocument current, string backupPath)
    {
        var backup = FlightStickDocument.Load(backupPath, current.Device);
        var session = new EditSession(current);
        foreach (var (key, value) in backup.Bindings)
            if (session.Current.ContainsKey(key)) session.Current[key] = value;
        return session;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string ChangedFileMessage() => T("文件在外部发生变化，请重新读取。", "The file changed on disk. Reload it before saving.", "ファイルが外部で変更されました。再読み込みしてから保存してください。");
}
