﻿using Sparrow.Geom;
using Sparrow.Utils;
using Sparrow.Rendering;
using Sparrow.Styles;
using System;

namespace Sparrow.Display
{
    /// <summary>
    /// Combines a number of meshes to one display object and renders them efficiently.
    ///
    /// <para>The most basic tangible (non-container) display object in Sparrow is the Mesh.
    /// However, a mesh typically does not render itself; it just holds the data describing its
    /// geometry. Rendering is orchestrated by the "MeshBatch" class. As its name suggests, it
    /// acts as a batch for an arbitrary number of Mesh instances; add meshes to a batch and they
    /// are all rendered together, in one draw call.</para>
    ///
    /// <para>You can only batch meshes that share similar properties, e.g. they need to have the
    /// same texture and the same blend mode. The first object you add to a batch will decide
    /// this state; call <code>canAddMesh</code> to find out if a new mesh shares that state.
    /// To reset the current state, you can call <code>clear</code>; this will also remove all
    /// geometry that has been added thus far.</para>
    ///
    /// <para>Sparrow will use MeshBatch instances (or compatible objects) for all rendering.
    /// However, you can also instantiate MeshBatch instances yourself and add them to the display
    /// tree. That makes sense for an object containing a large number of meshes; that way, that
    /// object can be created once and then rendered very efficiently, without having to copy its
    /// vertices and indices between buffers and GPU memory.</para>
    /// <see cref="Mesh"/>
    /// <see cref="Sprite"/>
    /// </summary>
    public class MeshBatch : Mesh
    {
        /// <summary>
        /// The maximum number of vertices that fit into one MeshBatch.
        /// </summary>
        public static readonly int MaxNumVertices = 65535;

        private MeshEffect _effect;
        private bool _batchable;
        private bool _vertexSyncRequired;
        private bool _indexSyncRequired;

        // helper object
        private static readonly MeshSubset SFullMeshSubset = new MeshSubset();

        /// <summary>
        /// Creates a new, empty MeshBatch instance.
        /// </summary>
        public MeshBatch() : base( new VertexData(), new IndexData()) { }

        // display object overrides

        public override void Dispose()
        {
            _effect?.Dispose();
            base.Dispose();
        }

        private void SetVertexAndIndexDataChanged()
        {
            _vertexSyncRequired = _indexSyncRequired = true;
        }

        private void SyncVertexBuffer()
        {
            _effect.UploadVertexData(_vertexData);
            _vertexSyncRequired = false;
        }

        private void SyncIndexBuffer()
        {
            _effect.UploadIndexData(_indexData);
            _indexSyncRequired = false;
        }

        /// <summary>
        /// Removes all geometry.
        /// </summary>
        public void Clear()
        {
            if (_parent != null) SetRequiresRedraw();

            _vertexData.NumVertices = 0;
            _indexData.NumIndices   = 0;
            _vertexSyncRequired = true;
            _indexSyncRequired  = true;
        }
        
        /// <summary>
        /// Adds a mesh to the batch by appending its vertices and indices.
        /// </summary>
        /// <param name="mesh">The mesh to add to the batch.</param>
        /// <param name="matrix">Transform all vertex positions with a certain matrix. If this
        ///                  parameter is omitted, <code>mesh.transformationMatrix</code>
        ///                  will be used instead (except if the last parameter is enabled).</param>
        /// <param name="alpha">Will be multiplied with each vertex' alpha value.</param>
        /// <param name="subset">The subset of the mesh you want to add, or <code>null</code> for
        ///                  the complete mesh.</param>
        /// <param name="ignoreTransformations">When enabled, the mesh's vertices will be added
        ///                  without transforming them in any way (no matter the value of the
        ///                  <code>matrix</code>)</param>
        public void AddMesh(Mesh mesh, Matrix2D matrix = null, float alpha = 1.0f,
                            MeshSubset subset = null, bool ignoreTransformations = false)
        {
            if (ignoreTransformations) matrix = null;
            else if (matrix == null) matrix = mesh.TransformationMatrix;
            if (subset == null) subset = SFullMeshSubset;

            int targetVertexId = _vertexData.NumVertices;
            int targetIndexId = _indexData.NumIndices;
            MeshStyle meshStyle = mesh._style;

            if (targetVertexId == 0)
            {
                SetupFor(mesh);
            }
            
            meshStyle.BatchVertexData(_style, targetVertexId, matrix, subset.VertexId, subset.NumVertices);
            meshStyle.BatchIndexData(_style, targetIndexId, targetVertexId - subset.VertexId,
                subset.IndexId, subset.NumIndices);

            if (alpha != 1.0f) _vertexData.ScaleAlphas(alpha, targetVertexId, subset.NumVertices);
            if (_parent != null) SetRequiresRedraw();

            _indexSyncRequired = _vertexSyncRequired = true;
        }

        /// <summary>
        /// Adds a mesh to the batch by copying its vertices and indices to the given positions.
        /// Beware that you need to check for yourself if those positions make sense; for example,
        /// you need to make sure that they are aligned within the 3-indices groups making up
        /// the mesh's triangles.
        ///
        /// <para>It's easiest to only add objects with an identical setup, e.g. only quads.
        /// For the latter, indices are aligned in groups of 6 (one quad requires six indices),
        /// and the vertices in groups of 4 (one vertex for every corner).</para>
        /// </summary>
        public void AddMeshAt(Mesh mesh, int indexId, int vertexId)
        {
            int numIndices = mesh.NumIndices;
            int numVertices = mesh.NumVertices;
            Matrix2D matrix = mesh.TransformationMatrix;
            MeshStyle meshStyle = mesh._style;

            if (_vertexData.NumVertices == 0)
            {
                SetupFor(mesh);
            }
            
            meshStyle.BatchVertexData(_style, vertexId, matrix, 0, numVertices);
            meshStyle.BatchIndexData(_style, indexId, vertexId, 0, numIndices);
           
            if (Alpha != 1.0f) _vertexData.ScaleAlphas(Alpha, vertexId, numVertices);
            if (_parent != null) SetRequiresRedraw();

            _indexSyncRequired = _vertexSyncRequired = true;
        }

        private void SetupFor(Mesh mesh)
        {
            MeshStyle meshStyle = mesh._style;
            Type meshStyleType = meshStyle.Type;

            if (_style.Type != meshStyleType)
            {
                SetStyle((MeshStyle)Activator.CreateInstance(meshStyleType), false);
            }
            _style.CopyFrom(meshStyle);
        }
        
        /// <summary>
        /// Indicates if the given mesh instance fits to the current state of the batch.
        /// Will always return <code>true</code> for the first added mesh; later calls
        /// will check if the style matches and if the maximum number of vertices is not
        /// exceeded.
        /// </summary>
        /// <param name="mesh">The mesh to add to the batch.</param>
        /// <param name="numVertices">If <code>-1</code>, <code>mesh.numVertices</code> will be used</param>
        public bool CanAddMesh(Mesh mesh, int numVertices= -1)
        {
            int currentNumVertices = _vertexData.NumVertices;

            if (currentNumVertices == 0) return true;
            if (numVertices< 0) numVertices = mesh.NumVertices;
            if (numVertices == 0) return true;
            if (numVertices + currentNumVertices > MaxNumVertices) return false;

            return _style.CanBatchWith(mesh._style);
        }

        /// <summary>
        /// If the <code>batchable</code> property is enabled, this method will add the batch
        /// to the painter's current batch. Otherwise, this will actually do the drawing.
        /// </summary>
        public override void Render(Painter painter)
        {
            if (_vertexData.NumVertices == 0)
            {
                return;
            }
            if (_pixelSnapping)
            {
                MatrixUtil.SnapToPixels(painter.State.ModelviewMatrix, painter.PixelSize);
            }

            if (_batchable)
            {
                painter.BatchMesh(this);
            }
            else
            {
                painter.FinishMeshBatch();
                painter.DrawCount += 1;
                painter.PrepareToDraw();
                painter.ExcludeFromCache(this);

                if (_vertexSyncRequired)
                {
                    SyncVertexBuffer();
                }
                if (_indexSyncRequired)
                {
                    SyncIndexBuffer();
                }

                _style.UpdateEffect(_effect, painter.State);
                _effect.Render(_indexData.NumTriangles);
            }
        }
        
        public override void SetStyle(MeshStyle meshStyle = null,
                                      bool mergeWithPredecessor = true)
        {
            base.SetStyle(meshStyle, mergeWithPredecessor);

            _effect?.Dispose();

            _effect = Style.CreateEffect();
            _effect.OnRestore = SetVertexAndIndexDataChanged;
        }

        /// <summary>
        /// The total number of vertices in the mesh. If you change this to a smaller value,
        /// the surplus will be deleted. Make sure that no indices reference those deleted
        /// vertices!
        /// </summary>
        public override int NumVertices
        {
            set
            {
                if (_vertexData.NumVertices != value)
                {
                    _vertexData.NumVertices = value;
                    _vertexSyncRequired = true;
                    SetRequiresRedraw();
                }
            }
        }

        /// <summary>
        /// The total number of indices in the mesh. If you change this to a smaller value,
        /// the surplus will be deleted. Always make sure that the number of indices
        /// is a multiple of three!
        /// </summary>
        public override int NumIndices
        {
            set
            {
                if (_indexData.NumIndices != value)
                {
                    _indexData.NumIndices = value;
                    _indexSyncRequired = true;
                    SetRequiresRedraw();
                }
            }
        }

        /// <summary>
        /// Indicates if this object will be added to the painter's batch on rendering,
        /// or if it will draw itself right away.
        ///
        /// <para>Only batchable meshes can profit from the render cache; but batching large meshes
        /// may take up a lot of CPU time. Activate this property only if the batch contains just
        /// a handful of vertices (say, 20 quads).</para>
        ///
        /// @default false
        /// </summary>
        public bool Batchable
        {
            set
            {
                if (_batchable != value)
                {
                    _batchable = value;
                    SetRequiresRedraw();
                }
            }
            get
            {
                return _batchable;
            }
        }
    
}
}
