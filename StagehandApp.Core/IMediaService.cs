using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace StagehandApp.Core
{
    public enum MediaStatus
    {
        Unknown,
        Playing,
        Paused,
        Stopped
    }

    public interface IMediaService
    {
        /// <summary>
        /// Событие при изменении трека
        /// </summary>
        event Action<string, string, string, byte[]?>? OnTrackChanged;

        /// <summary>
        /// Инициализация контроллера
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Воспроизведение
        /// </summary>
        Task PlayAsync();

        /// <summary>
        /// Пауза
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Следующий трек
        /// </summary>
        Task NextAsync();

        /// <summary>
        /// Предыдущий трек
        /// </summary>
        Task PreviousAsync();

        /// <summary>
        /// Текущий статус воспроизведения
        /// </summary>
        MediaStatus CurrentStatus { get; }
    }

    public class MediaInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public byte[]? AlbumArt { get; set; }
        public MediaStatus Status { get; set; }
    }
}
