using System.Security.Cryptography;

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
        if (gameRunning?.Invoke() == true) throw new InvalidOperationException("请先完全退出《皇牌空战8》，再保存键位。");
        var document = session.Document;
        byte[] output = document.Build(session.Current);
        if (Hash(File.ReadAllBytes(document.Path)) != document.Hash)
            throw new InvalidOperationException("配置文件已被游戏或其他程序修改。请重新读取，再调整键位。");
        if (output.AsSpan().SequenceEqual(document.OriginalBytes)) return new(false, null, document);
        // Validate the complete proposed document before touching the original file.
        _ = FlightStickDocument.FromBytes(document.Path, output);
        string directory = System.IO.Path.GetDirectoryName(document.Path)!;
        string stem = System.IO.Path.GetFileNameWithoutExtension(document.Path);
        string suffix = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
        string backup = System.IO.Path.Combine(directory, $"{stem}.before-pxn-tool-{suffix}.ini.bak");
        string temp = System.IO.Path.Combine(directory, $".{stem}.{suffix}.tmp");
        File.Copy(document.Path, backup, false);
        if (Hash(File.ReadAllBytes(backup)) != document.Hash)
            throw new InvalidOperationException("备份时配置发生变化，未写入。请重新读取配置。");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(output);
                stream.Flush(true);
            }
            if (Hash(File.ReadAllBytes(document.Path)) != document.Hash)
                throw new InvalidOperationException("保存前检测到外部改动，未覆盖。请重新读取配置。");
            File.Replace(temp, document.Path, null);
            if (!File.ReadAllBytes(document.Path).AsSpan().SequenceEqual(output))
                throw new IOException("保存后校验失败，请从备份恢复。备份：" + backup);
            return new(true, backup, FlightStickDocument.Load(document.Path));
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
        var backup = FlightStickDocument.Load(backupPath);
        var session = new EditSession(current);
        foreach (var (key, value) in backup.Bindings)
            if (session.Current.ContainsKey(key)) session.Current[key] = value;
        return session;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
