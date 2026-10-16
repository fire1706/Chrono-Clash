using ChronoClash.Core.Map;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Peint un morceau de carte (512 × 512 pixels) dans une image : sol, eau, collines, herbe, fleurs,
/// rochers, ruines et arbres. Port du dessin du prototype HTML.
///
/// Pour changer l'aspect du terrain : les couleurs sont dans ThemeDef (cœur), les formes ci-dessous.
/// Plus tard, le graphiste pourra remplacer les arbres et rochers par des images (voir docs/ART_GUIDE.md).
/// </summary>
public static class ChunkPainter
{
    public const int Size = 512;

    /// <summary>Renvoie les pixels du morceau (cx, cy) au format RGBA, 4 octets par pixel.</summary>
    public static byte[] Paint(Terrain t, int cx, int cy)
    {
        var buf = new byte[Size * Size * 4];
        int x0 = cx * Size, y0 = cy * Size;
        for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var (r, g, b) = Ground(t, x0 + px, y0 + py);
                int k = (py * Size + px) * 4;
                buf[k] = ToByte(r); buf[k + 1] = ToByte(g); buf[k + 2] = ToByte(b); buf[k + 3] = 255;
            }
        GrassTufts(t, buf, cx, cy);
        if (t.Theme.Flowers) Flowers(t, buf, cx, cy);
        Rocks(t, buf, x0, y0);
        Trees(t, buf, x0, y0);
        return buf;
    }

    // ================= Couleur du sol =================

    private readonly record struct C3(float R, float G, float B);
    private static C3 Of(Rgb c) => new(c.R, c.G, c.B);
    private static C3 Mix(C3 a, C3 b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    private static byte ToByte(float v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

    /// <summary>Couleur d'un pixel du sol selon l'altitude, la végétation et le thème.</summary>
    private static (float, float, float) Ground(Terrain t, int x, int y)
    {
        var th = t.Theme;
        float h = t.HeightAt(x, y);
        float water = Terrain.WaterLevel, deep = t.DeepLevel;
        C3 c;
        if (h < water)
        {
            if (h >= deep)
            {
                // Eau peu profonde (ou glace en hiver) : plus claire près du bord.
                c = Mix(Of(th.WetA), Of(th.WetB), Clamp01((water - h) / (water - deep)));
                if (h > water - 0.004f) c = Mix(c, new C3(226, 236, 232), 0.5f); // écume du rivage
            }
            else
            {
                c = Mix(Of(th.Shallow), Of(th.Deep), 0.35f + Clamp01((deep - h) / 0.05f) * 0.65f);
                if (h > deep - 0.003f) c = Mix(c, new C3(180, 210, 215), 0.35f); // limite de l'eau profonde
            }
        }
        else if (h < water + 0.022f) c = Of(th.Sand);
        else
        {
            float d = t.DirtAt(x, y), f = t.ForestAt(x, y);
            c = Mix(Of(th.GrassA), Of(th.GrassB), Clamp01((d - 0.35f) / 0.3f));
            if (d > 0.63f) c = Mix(c, Of(th.Dirt), Math.Clamp((d - 0.63f) / 0.06f, 0, 0.75f));
            if (f > th.ForestThreshold - 0.04f) c = Mix(c, Of(th.Forest), Clamp01((f - th.ForestThreshold + 0.04f) / 0.08f));
            if (h < water + 0.032f) c = Mix(Of(th.Sand), c, (h - water - 0.022f) / 0.01f);
            if (h > t.HillLevel)
            {
                // Collines : plus claires, avec des courbes de niveau.
                c = Mix(c, new C3(178, 164, 106), Math.Clamp((h - t.HillLevel) / 0.05f, 0, 0.5f));
                float q = ((h - t.HillLevel) / 0.016f) % 1f;
                if (q < 0.09f) c = Mix(c, new C3(96, 84, 52), 0.4f);
            }
        }
        float grain = (t.VisualHash(x >> 1, y >> 1) - 0.5f) * 10; // léger grain pour éviter les aplats
        return (c.R + grain, c.G + grain, c.B + grain);
    }

    // ================= Petits outils de dessin dans l'image =================

    /// <summary>Mélange une couleur avec le pixel existant (alpha = opacité de 0 à 1).</summary>
    private static void Blend(byte[] buf, int x, int y, byte r, byte g, byte b, float alpha)
    {
        if ((uint)x >= Size || (uint)y >= Size) return;
        int k = (y * Size + x) * 4;
        buf[k] = (byte)(buf[k] + (r - buf[k]) * alpha);
        buf[k + 1] = (byte)(buf[k + 1] + (g - buf[k + 1]) * alpha);
        buf[k + 2] = (byte)(buf[k + 2] + (b - buf[k + 2]) * alpha);
    }

    /// <summary>Disque plein (ou ellipse si ry ≠ rx).</summary>
    private static void FillEllipse(byte[] buf, float cx, float cy, float rx, float ry, Rgb c, float alpha)
    {
        int x0 = (int)(cx - rx), x1 = (int)(cx + rx) + 1, y0 = (int)(cy - ry), y1 = (int)(cy + ry) + 1;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1) Blend(buf, x, y, c.R, c.G, c.B, alpha);
            }
    }

    private static void FillCircle(byte[] buf, float cx, float cy, float r, Rgb c, float alpha = 1f) => FillEllipse(buf, cx, cy, r, r, c, alpha);

    /// <summary>Polygone plein (test pair-impair pour chaque pixel de la boîte englobante).</summary>
    private static void FillPolygon(byte[] buf, float ox, float oy, float[] pts, float scale, float shiftX, float shiftY, Rgb c, float alpha)
    {
        int n = pts.Length / 2;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        var xs = new float[n]; var ys = new float[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = ox + pts[i * 2] * scale + shiftX; ys[i] = oy + pts[i * 2 + 1] * scale + shiftY;
            minX = Math.Min(minX, xs[i]); maxX = Math.Max(maxX, xs[i]); minY = Math.Min(minY, ys[i]); maxY = Math.Max(maxY, ys[i]);
        }
        for (int y = (int)minY; y <= (int)maxY + 1; y++)
            for (int x = (int)minX; x <= (int)maxX + 1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                    if ((ys[i] > py) != (ys[j] > py) && px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
                if (inside) Blend(buf, x, y, c.R, c.G, c.B, alpha);
            }
    }

    // ================= Détails =================

    private static void GrassTufts(Terrain t, byte[] buf, int cx, int cy)
    {
        int x0 = cx * Size, y0 = cy * Size;
        var c = t.Theme.Tuft;
        for (int i = 0; i < 340; i++)
        {
            float x = x0 + t.VisualHash(cx * 997 + i, cy * 31) * Size, y = y0 + t.VisualHash(cx * 13, cy * 577 + i) * Size;
            if (t.HeightAt(x, y) < Terrain.WaterLevel + 0.03f || t.IsForest(x, y)) continue;
            int lx = (int)(x - x0), ly = (int)(y - y0);
            for (int k = 0; k < 5; k++) { Blend(buf, lx - 2, ly - k, c.R, c.G, c.B, 0.4f); Blend(buf, lx, ly - k - 1, c.R, c.G, c.B, 0.45f); Blend(buf, lx + 2, ly - k, c.R, c.G, c.B, 0.4f); }
        }
    }

    private static readonly Rgb[] FlowerColors = { Rgb.Of(243, 227, 107), Rgb.Of(245, 241, 230), Rgb.Of(217, 138, 214), Rgb.Of(232, 115, 90) };

    private static void Flowers(Terrain t, byte[] buf, int cx, int cy)
    {
        int x0 = cx * Size, y0 = cy * Size;
        for (int i = 0; i < 70; i++)
        {
            float x = x0 + t.VisualHash(cx * 71 + i, cy * 13) * Size, y = y0 + t.VisualHash(cx * 17, cy * 91 + i) * Size;
            if (t.HeightAt(x, y) < Terrain.WaterLevel + 0.04f || t.IsForest(x, y) || t.DirtAt(x, y) > 0.45f) continue;
            FillCircle(buf, x - x0, y - y0, 1.6f, FlowerColors[(int)(t.VisualHash(i, cx + cy) * 4) & 3]);
        }
    }

    private static void Rocks(Terrain t, byte[] buf, int x0, int y0)
    {
        foreach (var rock in t.Rocks)
        {
            if (rock.X + rock.Radius < x0 - 10 || rock.X - rock.Radius > x0 + Size + 10 || rock.Y + rock.Radius < y0 - 10 || rock.Y - rock.Radius > y0 + Size + 10) continue;
            float lx = rock.X - x0, ly = rock.Y - y0;
            if (rock.IsRuinPillar)
            {
                // Pilier de ruine : bloc de pierre taillée (dessiné droit, sans rotation, pour rester simple).
                float h = rock.Radius, top = rock.Broken ? h * 0.3f : h;
                var square = new[] { -h, -h, h, -h, h, top, -h, top };
                FillPolygon(buf, lx, ly, square, 1, 4, 5, Rgb.Of(20, 25, 10), 0.3f);
                FillPolygon(buf, lx, ly, square, 1, 0, 0, Rgb.Of(207, 197, 173), 1f);
                FillPolygon(buf, lx, ly, new[] { -h + 2, -h + 2, h - 2, -h + 2, h - 2, -h + 6, -h + 2, -h + 6 }, 1, 0, 0, Rgb.Of(228, 220, 199), 1f);
                continue;
            }
            FillPolygon(buf, lx, ly, rock.Outline, 1f, 5, 6, Rgb.Of(20, 25, 10), 0.32f);                                // ombre
            FillPolygon(buf, lx, ly, rock.Outline, 1f, 0, 0, Rgb.Of(133, 127, 117), 1f);                                // rocher
            FillPolygon(buf, lx, ly, rock.Outline, 0.68f, -rock.Radius * 0.14f, -rock.Radius * 0.16f, Rgb.Of(160, 154, 143), 1f); // face éclairée
            FillPolygon(buf, lx, ly, rock.Outline, 0.32f, -rock.Radius * 0.25f, -rock.Radius * 0.28f, Rgb.Of(185, 179, 167), 1f); // reflet
        }
    }

    private static void Trees(Terrain t, byte[] buf, int x0, int y0)
    {
        const int spacing = 34;
        var th = t.Theme;
        for (int gy = (int)Math.Floor((y0 - 40) / (float)spacing); gy <= (int)Math.Ceiling((y0 + Size + 40) / (float)spacing); gy++)
            for (int gx = (int)Math.Floor((x0 - 40) / (float)spacing); gx <= (int)Math.Ceiling((x0 + Size + 40) / (float)spacing); gx++)
            {
                float tx = gx * spacing + t.VisualHash(gx, gy * 3) * spacing * 0.9f, ty = gy * spacing + t.VisualHash(gx * 7, gy) * spacing * 0.9f;
                float f = t.ForestAt(tx, ty);
                if (t.HeightAt(tx, ty) < Terrain.WaterLevel + 0.03f) continue;
                float density = f > th.ForestThreshold ? 0.55f + (f - th.ForestThreshold) * 5 : f > th.ForestThreshold - 0.1f ? 0.03f : 0;
                if (t.VisualHash(gx * 11, gy * 13) > density) continue;
                float r = 10 + t.VisualHash(gx * 5, gy * 9) * 9, lx = tx - x0, ly = ty - y0;
                bool variant = t.VisualHash(gx * 3, gy * 17) < 0.5f;
                FillEllipse(buf, lx + 6, ly + 7, r, r * 0.85f, Rgb.Of(15, 30, 10), 0.38f); // ombre
                switch (th.Trees)
                {
                    case TreeStyle.Pine:
                        FillCircle(buf, lx, ly, r, Rgb.Of(45, 74, 56));
                        FillCircle(buf, lx, ly, r * 0.66f, Rgb.Of(54, 90, 68));
                        FillCircle(buf, lx, ly, r * 0.33f, Rgb.Of(45, 74, 56));
                        FillCircle(buf, lx - r * 0.25f, ly - r * 0.3f, r * 0.22f, Rgb.Of(245, 248, 250), 0.85f); // neige
                        FillCircle(buf, lx + r * 0.3f, ly + r * 0.1f, r * 0.16f, Rgb.Of(245, 248, 250), 0.85f);
                        break;
                    case TreeStyle.Palm:
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i / 6f * MathF.PI * 2 + variant.GetHashCode();
                            for (int s = 1; s <= 4; s++) FillCircle(buf, lx + MathF.Cos(a) * r * s * 0.27f, ly + MathF.Sin(a) * r * s * 0.27f, 2.2f, Rgb.Of(79, 122, 46));
                        }
                        FillCircle(buf, lx, ly, 3, Rgb.Of(122, 90, 50));
                        break;
                    default:
                        FillCircle(buf, lx, ly, r, variant ? Rgb.Of(47, 90, 38) : Rgb.Of(54, 101, 43));
                        FillCircle(buf, lx - r * 0.22f, ly - r * 0.25f, r * 0.68f, variant ? Rgb.Of(63, 114, 48) : Rgb.Of(71, 125, 53));
                        FillCircle(buf, lx - r * 0.38f, ly - r * 0.42f, r * 0.3f, Rgb.Of(170, 210, 110), 0.28f); // reflet
                        break;
                }
            }
    }
}
