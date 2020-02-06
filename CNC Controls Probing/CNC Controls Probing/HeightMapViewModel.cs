using CNC.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNC.Controls.Probing
{
    class HeightMapViewModel : ViewModelBase
    {
        private double _minX, _minY, _maxX, _maxY, _gridSize;
        private HeightMap _heightMap = null;
        private Vector2 _lowerLeft, _upperRight;

        public double MinX { get { return _minX; } set { if (value != _minX) _minX = value; OnPropertyChanged(); } }
        public double MaxX { get { return _maxX; } set { if (value != _maxX) _maxX = value; OnPropertyChanged(); } }
        public double MinY { get { return _minY; } set { if (value != _minY) _minY = value; OnPropertyChanged(); } }
        public double MaxY { get { return _maxY; } set { if (value != _maxY) _maxY = value; OnPropertyChanged(); } }
        public Vector2 LowerLeft { get; private set; } = new Vector2(0d, 0d);
        public Vector2 UpperRight { get; private set; } = new Vector2(0d, 0d);
        public double GridSize { get { return _gridSize; } set { if (value != _gridSize) _gridSize = value; OnPropertyChanged(); } }
        public HeightMap HeightMap { get { return _heightMap; } set { if (value != _heightMap) _heightMap = value; OnPropertyChanged(); } }
    }
}
