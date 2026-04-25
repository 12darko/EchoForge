using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace EchoForge.Core.DTOs
{
    public partial class TextOverlayDto : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        public Guid Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set { if (_text != value) { _text = value; OnPropertyChanged(); } }
        }

        private string _fontFamily = "Inter";
        public string FontFamily
        {
            get => _fontFamily;
            set { if (_fontFamily != value) { _fontFamily = value; OnPropertyChanged(); } }
        }

        private double _fontSize = 48;
        public double FontSize
        {
            get => _fontSize;
            set { if (Math.Abs(_fontSize - value) > 0.001) { _fontSize = value; OnPropertyChanged(); } }
        }

        private string _color = "#FFFFFF";
        public string Color
        {
            get => _color;
            set { if (_color != value) { _color = value; OnPropertyChanged(); } }
        }

        private string _alignment = "Center";
        public string Alignment
        {
            get => _alignment;
            set { if (_alignment != value) { _alignment = value; OnPropertyChanged(); } }
        }

        private double _positionX = 0.5;
        public double PositionX
        {
            get => _positionX;
            set { if (Math.Abs(_positionX - value) > 0.0001) { _positionX = value; OnPropertyChanged(); } }
        }

        private double _positionY = 0.5;
        public double PositionY
        {
            get => _positionY;
            set { if (Math.Abs(_positionY - value) > 0.0001) { _positionY = value; OnPropertyChanged(); } }
        }

        private double? _startTime = null;
        public double? StartTime
        {
            get => _startTime;
            set { if (_startTime != value) { _startTime = value; OnPropertyChanged(); } }
        }

        private double? _endTime = null;
        public double? EndTime
        {
            get => _endTime;
            set { if (_endTime != value) { _endTime = value; OnPropertyChanged(); } }
        }

        private string _animation = "none";
        public string Animation
        {
            get => _animation;
            set { if (_animation != value) { _animation = value; OnPropertyChanged(); } }
        }

        private double _outlineThickness = 0;
        public double OutlineThickness
        {
            get => _outlineThickness;
            set { if (Math.Abs(_outlineThickness - value) > 0.001) { _outlineThickness = value; OnPropertyChanged(); } }
        }

        private double _shadowOpacity = 0;
        public double ShadowOpacity
        {
            get => _shadowOpacity;
            set { if (Math.Abs(_shadowOpacity - value) > 0.001) { _shadowOpacity = value; OnPropertyChanged(); } }
        }

        private double _transparency = 0;
        public double Transparency
        {
            get => _transparency;
            set { if (Math.Abs(_transparency - value) > 0.001) { _transparency = value; OnPropertyChanged(); } }
        }

        private bool _isSelected;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public TextOverlayDto Clone()
        {
            return new TextOverlayDto
            {
                Id = Guid.NewGuid(),
                Text = this.Text,
                FontFamily = this.FontFamily,
                FontSize = this.FontSize,
                Color = this.Color,
                Alignment = this.Alignment,
                PositionX = this.PositionX,
                PositionY = this.PositionY,
                Animation = this.Animation,
                OutlineThickness = this.OutlineThickness,
                ShadowOpacity = this.ShadowOpacity,
                Transparency = this.Transparency
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
