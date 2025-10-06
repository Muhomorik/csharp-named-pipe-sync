---
tags:
  - MagicNET
---
Magick.NET (ImageMagick for .NET) exposes a very large set of “one-call” operations — single methods you can call on a MagickImage (or via processing helpers) to perform common image tasks. Below is a focused list of common one-call operations you’ll likely use, each with a short description and typical method name(s). I also note how to restrict operations to a region and give a short code example.

Common one-call operations in Magick.NET

- Resize / Scale
  - Resize(width, height) / Scale(width, height) — resample the image to new dimensions.

- Crop / Trim
  - Crop(MagickGeometry) — extract a rectangular region.
  - Trim() — remove border of uniform color.

- Rotate / Flip / Flop
  - Rotate(angle) — rotate by degrees.
  - Flip() / Flop() — vertical/horizontal mirror.

- Blur / Gaussian Blur / Motion Blur
  - Blur(radius, sigma) — basic blur.
  - GaussianBlur(sigma) or GaussianBlur(radius, sigma) — Gaussian smoothing.
  - MotionBlur(radius, sigma, angle) — directional blur.

- Sharpen / Unsharp Mask
  - Sharpen(radius, sigma) / UnsharpMask(radius, sigma, amount, threshold).

- Pixelate / Posterize / Mosaic
  - Pixelate(pixelSize) or use Morphology/Resize trick — ImageMagick exposes pixelate functionality (varies by version).
  - Posterize(levels) — reduce color levels.

- Color adjustments
  - Negate() — invert colors.
  - Modulate(brightness, saturation, hue) — adjust brightness/saturation/hue.
  - Colorize(color, percentage) — tint.
  - SepiaTone(threshold) — sepia effect.
  - Level(blackPoint, gamma, whitePoint) — level adjustments.
  - Normalize() / Equalize() — contrast normalization.

- Tone / Exposure
  - BrightnessContrast(brightness, contrast).
  - Contrast() / Contrast(true) to increase/decrease.

- Color reduction / quantization / dithering
  - Quantize(colors, colorspace, treedepth, ditherMethod, measureError) — perform color reduction.
  - ReduceNoise(radius) — denoise.

- Format conversion and encoding
  - Write(path) or ToByteArray() with a format parameter — saves/encodes to PNG/JPEG/GIF/... directly.

- Composite / Overlay / Draw
  - Composite(otherImage, CompositeOperator, x, y) — overlay another image with an operator.
  - Draw(Drawables) — draw shapes/text.

- Border / Frame / Padding
  - Border(color, width, height) / Frame(...) — add frame or border.

- Distortions and geometry
  - Distort(...) for perspective/affine transforms.
  - Shear(x,y) — skew.
  - PerspectiveTransform(...) — perspective warping.

- Morphology and filtering
  - Morphology(method, kernel) — morphology ops (dilate/erode/open/close).
  - Convolve(kernel) — custom convolution.

- Special effects
  - Swirl(degrees), Wave(amplitude, wavelength), OilPaint(radius), Charcoal(sigma), Implode(amount).

- Animation / GIF-specific
  - Coalesce(), Optimize(), Animate-related helpers and frame control.

- Metadata / EXIF / Profiles
  - RemoveProfile(name) / GetExifProfile() / SetAttribute() / GetAttribute() — read/write metadata.

- Masks and transparency
  - Transparent(color) / Opaque(color1, color2) / SetMask().

- Morphing / Merging frames
  - Montage(), Append(), Combine() helpers.

Examples and region handling
- Apply to a region: crop to the region, apply operation, then composite back.
  - crop = image.Clone(); crop.Crop(geom); crop.Pixelate(size); image.Composite(crop, x, y, CompositeOperator.Over);

Short code examples

Pixelate a rectangle (one-call pixelate + composite back)
```csharp
using (var img = new MagickImage("input.jpg"))
{
    var region = new MagickGeometry(x, y, width, height);
    using (var crop = new MagickImage(img))
    {
        crop.Crop(region);
        crop.Pixelate(pixelSize); // one-call if available in your version
        img.Composite(crop, x, y, CompositeOperator.Over);
    }
    img.Write("out.jpg");
}
```


Blur whole image
```csharp
using (var img = new MagickImage("in.png"))
{
    img.Blur(0, 5); // radius=0 (auto), sigma=5
    img.Write("blurred.png");
}
```


Resize + convert to PNG
```csharp
using (var img = new MagickImage("in.tif"))
{
    img.Resize(800, 600);
    img.Write("out.png");
}
```


Posterize / reduce colors
```csharp
using (var img = new MagickImage("in.jpg"))
{
    img.Posterize(16); // reduce to ~16 levels per channel
    img.Write("posterized.jpg");
}
```


Text drawing
```csharp
using (var img = new MagickImage("background.png"))
{
    var drawables = new Drawables()
        .FontPointSize(36)
        .FillColor(MagickColors.White)
        .TextAlignment(TextAlignment.Center)
        .Text(img.Width/2, img.Height/2, "Hello");
    drawables.Draw(img);
    img.Write("text.png");
}
```


Notes, caveats and tips
- Method names can vary slightly by Magick.NET build/version. Check IntelliSense or docs for exact overloads (e.g., Pixelate vs. Morphology-based approaches).
- Many operations are in-place on MagickImage; chain them or call Clone() if you need to keep originals.
- Magick.NET uses native ImageMagick binaries; watch deployment size and choose an appropriate platform-specific build (x86/x64).
- For region operations you commonly Crop/Cut the region, operate on the crop, then Composite back to the source.
- Magick.NET exposes low-level control (kernels, colorspaces, caches) if you need to tune performance or quality.

If you want, I can:
- paste a ready-to-run Magick.NET method that accepts an image and a rectangle and applies a chosen one-call operation (pixelate, blur, sepia, etc.) and returns a WPF BitmapSource; or
- search exact Magick.NET method names for your installed NuGet version and provide copy-paste examples. Which would you prefer?