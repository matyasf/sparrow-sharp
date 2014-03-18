using System;
using Sparrow.Geom;
using OpenTK;

namespace Sparrow.Utils
{
	public class VertexData
	{
		private const float MIN_ALPHA = 5.0f / 255.0f;
		private Vertex[] _vertices;
		private VertexColor[] _vertexColors;
		private int _numVertices;
		private bool _premultipliedAlpha;

		public int NumVertices {
			get { return _numVertices; }
			set { 
				if (value != _numVertices) {
					if (value > 0) {
						if (_vertices != null) {
							Array.Resize (ref _vertices, value);
							Array.Resize (ref _vertexColors, value);
						} else {
							_vertices = new Vertex[value];
							_vertexColors = new VertexColor[value];
						}

						if (value > _numVertices) {
							for (int i = _numVertices; i < value; i++) {
								_vertexColors [i] = VertexColorHelper.CreateVertexColor (0, 1.0f);
							}
						}
					} else {
						_vertices = null;
					}

					_numVertices = value;
				}
			}
		}

		public Rectangle Bounds {
			get { return BoundsAfterTransformation (null, 0, _numVertices); }
		}

		public Vertex[] Vertices {
			get { return _vertices; }
		}

		public VertexColor[] VertexColors {
			get { return _vertexColors; }
		}

		public bool Tinted {
			get {
				for (int i = 0; i < _numVertices; ++i)
					if (!VertexColorHelper.IsOpaqueWhite (_vertexColors [i]))
						return true;

				return false;
			}
		}

		public bool PremultipliedAlpha {
			get {
				return _premultipliedAlpha;
			}
			set {
				SetPremultipliedAlpha (value, true);
			}
		}

		public VertexData (int numVertices = 0, bool premultipliedAlpha = false)
		{
			_premultipliedAlpha = premultipliedAlpha;
			NumVertices = numVertices;
		}

		public void CopyToVertexData (VertexData target, bool copyColor)
		{
			CopyToVertexData (target, 0, _numVertices, copyColor);
		}

		public void CopyToVertexData (VertexData target, int atIndex, bool copyColor)
		{
			CopyToVertexData (target, atIndex, _numVertices, copyColor);
		}

		public void CopyToVertexData (VertexData target, int atIndex, int numVertices, bool copyColor)
		{
			Vertex.Copy (_vertices, 0, target.Vertices, atIndex, numVertices);

			if (copyColor) {
				Array.Copy (_vertexColors, 0, target.VertexColors, atIndex, numVertices);
			}
		}

		public Point PositionAtIndex (int index)
		{
			if (index < 0 || index >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			Vector2 position = _vertices [index].Position;

			return Point.Create (position.X, position.Y);
		}

		public void SetPosition (Point position, int atIndex)
		{
			if (atIndex < 0 || atIndex >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			_vertices [atIndex].Position = new Vector2 (position.X, position.Y);
		}

		public void SetPosition (float x, float y, int atIndex)
		{
			if (atIndex < 0 || atIndex >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			_vertices [atIndex].Position = new Vector2 (x, y);
		}

		public Point TexCoordsAtIndex (int index)
		{
			if (index < 0 || index >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			Vector2 texCoords = _vertices [index].TexCoords;
			return Point.Create (texCoords.X, texCoords.Y);
		}

		public void SetTexCoords (Point texCoords, int atIndex)
		{
			if (atIndex < 0 || atIndex >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			_vertices [atIndex].TexCoords = new Vector2 (texCoords.X, texCoords.Y);
		}

		public void SetTexCoords (float x, float y, int atIndex)
		{
			if (atIndex < 0 || atIndex >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			_vertices [atIndex].TexCoords = new Vector2 (x, y);
		}

		public void SetColor (uint color, float alpha, int atIndex)
		{
			if (atIndex < 0 || atIndex >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			alpha = NumberUtil.Clamp (alpha, _premultipliedAlpha ? MIN_ALPHA : 0.0f, 1.0f); 

			VertexColor vertexColor = VertexColorHelper.CreateVertexColor (color, alpha);
			_vertexColors [atIndex] = _premultipliedAlpha ? VertexColorHelper.PremultiplyAlpha (vertexColor) : vertexColor;
		}

		public void SetColor (uint color, float alpha)
		{
			for (int i = 0; i < _numVertices; i++) {
				SetColor (color, alpha, i);
			}
		}

		public uint ColorAtIndex (int index)
		{
			if (index < 0 || index >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			VertexColor vertexColor = _vertexColors [index];
			if (_premultipliedAlpha) {
				vertexColor = VertexColorHelper.UnmultiplyAlpha (vertexColor);
			}

			return ColorUtil.GetRGB (vertexColor.R, vertexColor.G, vertexColor.B);
		}

		public void SetColor (uint color, int atIndex)
		{
			float alpha = AlphaAtIndex (atIndex);
			SetColor (color, alpha, atIndex);
		}

		public void SetColor (uint color)
		{
			for (int i = 0; i < _numVertices; i++) {
				SetColor (color, i);
			}
		}

		public void SetAlpha (float alpha, int atIndex)
		{
			uint color = ColorAtIndex (atIndex);
			SetColor (color, alpha, atIndex);
		}

		public void SetAlpha (float alpha)
		{
			for (int i = 0; i < _numVertices; i++) {
				SetAlpha (alpha, i);
			}
		}

		public float AlphaAtIndex (int index)
		{
			if (index < 0 || index >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			//            return _vertices[index].Color.A / 255.0f;
			return 1.0f;
		}

		public void ScaleAlphaBy (float factor)
		{
			ScaleAlphaBy (factor, 0, _numVertices);
		}

		public void ScaleAlphaBy (float factor, int index, int numVertices)
		{
			if (index < 0 || index >= _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			if (factor == 1.0f) {
				return;
			}

			int minAlpha = _premultipliedAlpha ? (int)(MIN_ALPHA * 255.0) : 0;

			for (int i = index; i < index + numVertices; ++i) {
				VertexColor vertexColor = _vertexColors [i];
				byte newAlpha = Convert.ToByte (NumberUtil.Clamp (vertexColor.A * factor, minAlpha, 255));

				if (_premultipliedAlpha) {
					vertexColor = VertexColorHelper.UnmultiplyAlpha (vertexColor);
					vertexColor.A = newAlpha;
					_vertexColors [i] = VertexColorHelper.PremultiplyAlpha (vertexColor);
				} else {
					_vertexColors [i] = VertexColorHelper.CreateVertexColor (vertexColor.R, vertexColor.G, vertexColor.B, newAlpha);
				}
			}
		}

		public void AppendVertex (Vertex vertex)
		{
			// TODO: implement
			//            NumVertices += 1;
			//
			//            if (_premultipliedAlpha)
			//            {
			//                vertex.Color = VertexColorHelper.PremultiplyAlpha(vertex.Color);
			//            }
			//            _vertices[_numVertices - 1] = vertex;
		}

		public void TransformVerticesWithMatrix (Matrix matrix, int atIndex, int numVertices)
		{
			if (matrix == null) {
				return;
			}

			for (int i = atIndex, end = atIndex + numVertices; i < end; ++i) {
				Vector2 pos = _vertices [i].Position;
				float x = matrix.A * pos.X + matrix.C * pos.Y + matrix.Tx;
				float y = matrix.D * pos.Y + matrix.B * pos.X + matrix.Ty;

				_vertices [i].Position.X = x;
				_vertices [i].Position.Y = y;
			}
		}

		public Rectangle BoundsAfterTransformation (Matrix matrix)
		{
			return BoundsAfterTransformation (matrix, 0, _numVertices);
		}

		public Rectangle BoundsAfterTransformation (Matrix matrix, int atIndex, int numVertices)
		{
			if (atIndex < 0 || atIndex + numVertices > _numVertices) {
				throw new IndexOutOfRangeException ("Invalid vertex index");
			}

			if (numVertices == 0)
				return null;

			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float minY = float.MaxValue;
			float maxY = float.MinValue;

			int endIndex = atIndex + numVertices;

			if (matrix != null) {
				for (int i = atIndex; i < endIndex; ++i) {
					Vector2 position = _vertices [i].Position;
					Point transformedPoint = matrix.TransformPoint (position.X, position.Y);
					float tfX = transformedPoint.X;
					float tfY = transformedPoint.Y;
					minX = Math.Min (minX, tfX);
					maxX = Math.Max (maxX, tfX);
					minY = Math.Min (minY, tfY);
					maxY = Math.Max (maxY, tfY);
				}
			} else {
				for (int i = atIndex; i < endIndex; ++i) {
					Vector2 position = _vertices [i].Position;
					minX = Math.Min (minX, position.X);
					maxX = Math.Max (maxX, position.X);
					minY = Math.Min (minY, position.Y);
					maxY = Math.Max (maxY, position.Y);
				}
			}

			return new Rectangle (minX, minY, maxX - minX, maxY - minY);
		}

		public void SetPremultipliedAlpha (bool value, bool updateVertices)
		{
			if (value == _premultipliedAlpha) {
				return;
			}

			if (updateVertices) {
				if (value) {
					for (int i = 0; i < _numVertices; ++i) {
						_vertexColors [i] = VertexColorHelper.PremultiplyAlpha (_vertexColors [i]);
					}
				} else {
					for (int i = 0; i < _numVertices; ++i) {
						_vertexColors [i] = VertexColorHelper.UnmultiplyAlpha (_vertexColors [i]);
					}
				}
			}

			_premultipliedAlpha = value;
		}
	}
}