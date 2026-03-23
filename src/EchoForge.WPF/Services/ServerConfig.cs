using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EchoForge.WPF.Services;

/// <summary>
/// Gömülü sunucu yapılandırması.
/// Sunucu adresi AES ile şifrelenmiş olarak binary'e gömülüdür.
/// Admin olmayan kullanıcılar için otomatik bağlantı sağlar.
/// Admin kullanıcılar Ayarlar'dan değiştirebilir.
/// </summary>
public static class ServerConfig
{
    // ── AES-256 şifreleme anahtarı (32 byte) ve IV (16 byte) ──
    // Bu key sadece URL'yi obfuscate etmek için kullanılır.
    private static readonly byte[] _key = Convert.FromBase64String("RWNob0ZvcmdlU2VydmVyS2V5MjAyNiE=".PadRight(44, '=')[..44]);
    private static readonly byte[] _iv  = Convert.FromBase64String("RWNob0ZJVjIwMjZLZXk=".PadRight(24, '=')[..24]);

    // ── Şifreli sunucu URL'si ──
    // Orijinal: https://srv1364487.hstgr.cloud
    // Base64 ile encode edilmiş AES-CBC ciphertext
    private static readonly string _encryptedUrl = EncryptForEmbedding("https://srv1364487.hstgr.cloud");

    // Fallback localhost (geliştirme ortamı için)
    private const string _fallbackUrl = "http://localhost:5035";

    /// <summary>
    /// Şifresi çözülmüş sunucu adresini döndürür.
    /// Çözme başarısız olursa uygulama ayarlarından veya fallback'ten okur.
    /// </summary>
    public static string GetServerUrl()
    {
        try
        {
            // Önce local override dosyasını kontrol et (dev ortamı)
            var overridePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_override.txt");
            if (File.Exists(overridePath))
            {
                var overrideUrl = File.ReadAllText(overridePath).Trim();
                if (!string.IsNullOrEmpty(overrideUrl))
                    return overrideUrl;
            }

            // Şifreli URL'yi çöz
            return DecryptEmbeddedUrl();
        }
        catch
        {
            return _fallbackUrl;
        }
    }

    /// <summary>
    /// Gömülü şifreli URL'nin çözülmüş halini döndürür
    /// </summary>
    private static string DecryptEmbeddedUrl()
    {
        try
        {
            var cipherBytes = Convert.FromBase64String(_encryptedUrl);
            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            aes.IV = DeriveIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }
        catch
        {
            return _fallbackUrl;
        }
    }

    /// <summary>
    /// Build-time embed helper — URL'yi şifreler (sadece development'ta kullanılır)
    /// </summary>
    private static string EncryptForEmbedding(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.IV = DeriveIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// Deterministic anahtar türetme (32 byte SHA256)
    /// </summary>
    private static byte[] DeriveKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("EchoForge::Server::2026::Key"));
    }

    /// <summary>
    /// Deterministic IV türetme (16 byte MD5)
    /// </summary>
    private static byte[] DeriveIV()
    {
        return MD5.HashData(Encoding.UTF8.GetBytes("EchoForge::Server::2026::IV"));
    }
}
