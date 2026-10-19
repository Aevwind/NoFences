using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace NoFences.Util
{
    /// <summary>为配置文件提供有大小限制的安全读取和保留备份的原子写入。</summary>
    internal static class XmlFileStore
    {
        /// <summary>读取有大小上限的 XML 文本，拒绝空文件和过大的配置。</summary>
        public static string ReadText(string path, long maximumBytes)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length == 0 || stream.Length > maximumBytes)
                    throw new InvalidDataException("配置文件大小无效。");
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }
        }

        /// <summary>反序列化受限文本，禁用 DTD 与外部实体解析。</summary>
        public static T Deserialize<T>(string text, long maximumBytes)
        {
            var options = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = maximumBytes
            };
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, options))
                return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }

        /// <summary>在同目录刷新临时文件后原子替换主文件，失败时保留主文件并清理临时文件。</summary>
        public static void Save<T>(string path, T value, long maximumBytes)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(T)).Serialize(stream, value);
                    if (stream.Length > maximumBytes)
                        throw new InvalidDataException("配置文件超过大小限制。");
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, fullPath + ".bak");
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Configuration cleanup failed: " + ex);
                }
            }
        }
    }
}
