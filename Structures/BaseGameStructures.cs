using System.Numerics;

namespace DotnesktRemastered.Structures
{
    public interface IGfxWorld<out TGfxWorldSurfaces, out TGfxWorldStaticModels>
        where TGfxWorldSurfaces : unmanaged, IGfxWorldSurfaces
        where TGfxWorldStaticModels : unmanaged, IGfxWorldStaticModels
    {
        public nint baseName { get; }
        public uint transientZoneCount { get; }
        public ulong[] transientZones { get; }
        public TGfxWorldSurfaces surfaces { get; }
        public TGfxWorldStaticModels smodels { get; }
    }

    public interface IMaterial
    {
        public ulong hash { get; }
        public byte textureCount{ get; }
        public byte imageCount{ get; }
        public nint textureTable{ get; }
        public nint imageTable{ get; }
    }

    public interface IGfxImage
    {
        public ulong hash { get; }
    }

    public interface IGfxWorldSurfaces
    {
        public uint count { get; }
        public nint surfaces { get; }
        public nint materials { get; }
        public nint ugbSurfData { get; }
    }

    public interface IGfxWorldDrawVerts
    {
        public uint posSize { get; }
        public nint posData { get; }
        public nint indices { get; }
        public nint tableData { get; }
        public nint packedIndices { get; }
    }

    public interface IGfxWorldTransientZone<TGfxWorldDrawVerts>
        where TGfxWorldDrawVerts : IGfxWorldDrawVerts
    {
        public ulong hash { get; }
        public TGfxWorldDrawVerts drawVerts { get; }
    }

    public interface IGfxSurface
    {
        public uint baseIndex { get; }
        public uint tableIndex { get; }
        public uint ugbSurfDataIndex { get; }
        public uint materialIndex { get; }
        public ushort triCount { get; }
        public ushort packedIndicesTableCount { get; }
        public ushort vertexCount { get; }
        public uint packedIndicesOffset { get; }
    }

    public interface IGfxUgbSurfData<out TGfxWorldDrawOffset>
    {
        public TGfxWorldDrawOffset worldDrawOffset { get; }
        public uint transientZoneIndex { get; }
        public uint layerCount { get; }
        public uint xyzOffset { get; }
        public uint tangentFrameOffset { get; }
        public uint colorOffset { get; }
        public uint texCoordOffset { get; }
    }

    public interface IGfxWorldStaticModels
    {
        public uint collectionsCount { get; }
        public nint collections { get; }
        public nint smodels { get; }
        public nint instanceData { get; }
    }

    public interface IGfxStaticModelCollection
    {
        public uint firstInstance { get; }
        public uint instanceCount { get; }
        public ushort smodelIndex { get; }
        public ushort transientGfxWorldPlaced { get; }
    }

    public interface IGfxStaticModel
    {
        public nint xmodel { get; }
    }

    public interface IXModel
    {
        public ulong hash { get; }
        public nint name { get; }
        public nint materialHandles { get; }
        public nint lodInfo { get; }
    }

    public interface IXModelLod
    {
        public nint MeshPtr { get; }
    }

    public interface IXModelSurfs
    {
        public nint surfs { get; }
        public nint shared { get; }
        public ulong xpakKey { get; }
    }

    public interface IXSurfaceShared
    {
        public nint data { get; }
        public uint dataSize { get; }
        public ulong xpakKey { get; }
    }

    public interface IGfxSModelInstanceData
    {
        public int[] translation { get; }
        public ushort[] orientation { get; }
        public ushort halfFloatScale { get; }
    }

    public interface IGfxWorldDrawOffset
    {
        public float x { get; }
        public float y { get; }
        public float z { get; }
        public float scale { get; }
    }

    public interface IXSurface
    {
        public uint packedIndicesTableCount { get; }
        public uint vertCount { get; }
        public uint triCount { get; }
        public uint xyzOffset { get; }
        public uint texCoordOffset { get; }
        public uint tangentFrameOffset { get; }
        public uint indexDataOffset { get; }
        public uint packedIndiciesTableOffset { get; }
        public uint packedIndicesOffset { get; }
        public uint colorOffset { get; }
        public uint secondUVOffset { get; }
    }
}