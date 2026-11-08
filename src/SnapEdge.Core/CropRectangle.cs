namespace SnapEdge.Core;

public readonly record struct CropRectangle(int X, int Y, int Width, int Height)
{
    public static CropRectangle Full(int width, int height) => new(0, 0, width, height);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
