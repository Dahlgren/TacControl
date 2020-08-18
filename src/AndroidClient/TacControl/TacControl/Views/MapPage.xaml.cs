using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Mapsui;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia;
using Mapsui.Rendering.Skia.SkiaStyles;
using Mapsui.Styles;
using Mapsui.UI;
using Mapsui.UI.Forms;
using SkiaSharp;
using TacControl.Common;
using TacControl.Common.Modules;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using TacControl.Models;
using TacControl.Views;
using TacControl.ViewModels;
using Bitmap = Mapsui.Styles.Bitmap;
using Color = Svg.Picture.Color;
using Image = Xamarin.Forms.Image;
using Point = Xamarin.Forms.Point;

namespace TacControl.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MapPage : ContentPage
    {
        public Func<MapControl, MapClickedEventArgs, bool> Clicker { get; set; }

        private Layer GPSTrackerLayer = new Mapsui.Layers.Layer("GPS Trackers");
        private Layer MapMarkersLayer = new Mapsui.Layers.Layer("Map Markers");
        public static BoundingBox currentBounds = new Mapsui.Geometries.BoundingBox(0, 0, 0, 0);



        public MapPage()
        {
            InitializeComponent();

            MapControl.RotationLock = true;

            //MapControl.TouchStarted += MapControlOnMouseLeftButtonDown;
            MapControl_OnLoaded();
        }

        //public MapPage(Action<IMapControl> setup, Func<MapControl, MapClickedEventArgs, bool> c = null)
        //{
        //    InitializeComponent();
        //
        //    MapControl.RotationLock = false;
        //    MapControl.UnSnapRotationDegrees = 30;
        //    MapControl.ReSnapRotationDegrees = 5;
        //    MapControl.Info += MapControl_Info;
        //
        //    setup(MapControl);
        //
        //    Clicker = c;
        //   
        //}

        protected override void OnAppearing()
        {
            MapControl.IsVisible = true;
            MapControl.Refresh();
        }

        private void MapControl_Info(object sender, Mapsui.UI.MapInfoEventArgs e)
        {
            if (e?.MapInfo?.Feature != null)
            {
                foreach (var style in e.MapInfo.Feature.Styles)
                {
                    if (style is CalloutStyle)
                    {
                        style.Enabled = !style.Enabled;
                        e.Handled = true;
                    }
                }

                MapControl.Refresh();
            }
        }







        private static readonly XNamespace xlink = "http://www.w3.org/1999/xlink";
        private static readonly XNamespace svg = "http://www.w3.org/2000/svg";
        private readonly XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();


        public struct SvgLayer
        {
            public string name;
            public string content;
        }

        public List<SvgLayer> ParseLayers()
        {
            List<SvgLayer> ret = new List<SvgLayer>();

           

            var wantedDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            string filePath = "";
            if (File.Exists(Path.Combine(wantedDirectory, GameState.Instance.gameInfo.worldName + ".svgz")))
                filePath = Path.Combine(wantedDirectory, GameState.Instance.gameInfo.worldName + ".svgz");
            else if (File.Exists(Path.Combine(wantedDirectory, GameState.Instance.gameInfo.worldName + ".svg")))
                filePath = Path.Combine(wantedDirectory, GameState.Instance.gameInfo.worldName + ".svg");

            if (string.IsNullOrEmpty(filePath))
            {
                ImageDirectory.Instance.RequestMapfile(GameState.Instance.gameInfo.worldName, wantedDirectory)
                    .ContinueWith(
                        (x) =>
                        {
                            MapControl_OnLoaded();
                        });
                return ret;
            }

            bool isCompressed = filePath.EndsWith("z");

            using (var fileStream = File.OpenRead(filePath))
            {
                GZipStream decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress);

                Stream stream = isCompressed ? decompressionStream : (Stream)fileStream;

                using (var reader = XmlReader.Create(stream, xmlReaderSettings, CreateSvgXmlContext()))
                {
                    var xdoc = XDocument.Load(reader);
                    var svg = xdoc.Root;
                    var ns = svg.Name.Namespace;

                    var mainAttributes = svg.Attributes();
                    var defs = svg.Element("defs");
                    List<XElement> layers = new List<XElement>();
                    List<XElement> rootElements = new List<XElement>();

                    foreach (var xElement in svg.Elements())
                    {
                        if (xElement.Name == ns + "g")
                        {
                            layers.Add(xElement);
                        }
                        else
                            rootElements.Add(xElement);
                    }

                    XDocument bareDoc = new XDocument(xdoc);
                    List<XElement> toRemove = new List<XElement>();
                    foreach (var xElement in bareDoc.Root.Elements())
                    {
                        if (xElement.Name == ns + "g")
                            toRemove.Add(xElement);
                    }
                    toRemove.ForEach(x => x.Remove());

                    var test = bareDoc.ToString();
                    foreach (var xElement in layers)
                    {
                        XDocument newDoc = new XDocument(bareDoc);
                        newDoc.Root.Add(xElement);
                        SvgLayer x;
                        x.content = newDoc.ToString();
                        x.name = xElement.Attribute("id").Value;
                        ret.Add(x);
                    }



                }

                decompressionStream.Dispose();
            }

            return ret;
        }

        private static XmlParserContext CreateSvgXmlContext()
        {
            var table = new NameTable();
            var manager = new XmlNamespaceManager(table);
            manager.AddNamespace(string.Empty, svg.NamespaceName);
            manager.AddNamespace("xlink", xlink.NamespaceName);
            return new XmlParserContext(null, manager, null, XmlSpace.None);
        }

        private void MapControl_OnLoaded()
        {


            //MapControl.Map.Layers.Clear();
            //
            //
            //MapControl.Map = new Map();


            var layers = ParseLayers();
            if (layers.Count == 0) return;
            int terrainWidth = 0;
            foreach (var svgLayer in layers)
            {

                var layer = new Mapsui.Layers.ImageLayer(svgLayer.name);


                if (svgLayer.name == "forests" || svgLayer.name == "countLines" || svgLayer.name == "rocks" || svgLayer.name == "grid")
                {
                    layer.Enabled = false;
                }

                var head = svgLayer.content.Substring(0, svgLayer.content.IndexOf('\n'));
                var widthSub = head.Substring(head.IndexOf("width"));
                var width = widthSub.Substring(7, widthSub.IndexOf('"', 7) - 7);
                terrainWidth = int.Parse(width);

                currentBounds = new Mapsui.Geometries.BoundingBox(0, 0, terrainWidth, terrainWidth);

                //
                var features = new Features();
                var feature = new Feature { Geometry = new BoundBox(currentBounds), ["Label"] = svgLayer.name };

                var x = new SvgStyle { image = new Svg.Skia.SKSvg() };
          
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgLayer.content)))
                {
                    //using (var reader = XmlReader.Create(stream, xmlReaderSettings, CreateSvgXmlContext())) {
                        x.image.Load(stream);
                    //}
                }


                feature.Styles.Add(x);
                features.Add(feature);

                //
                layer.DataSource = new MemoryProvider(features);
                MapControl.Map.Layers.Add(layer);
            }

            //var layer = new Mapsui.Layers.ImageLayer("Base");
            //layer.DataSource = CreateMemoryProviderWithDiverseSymbols();
            //MapControl.Map.Layers.Add(layer);


            var svgRender = new SvgStyleRenderer();
            MapControl.Renderer.StyleRenderers[typeof(SvgStyle)] = svgRender;
            MapControl.Map.Limiter = new ViewportLimiter();
            MapControl.Map.Limiter.PanLimits = new Mapsui.Geometries.BoundingBox(0, 0, terrainWidth, terrainWidth);

            GPSTrackerLayer.IsMapInfoLayer = true;
            GPSTrackerLayer.DataSource = new GPSTrackerProvider(GPSTrackerLayer);
            GPSTrackerLayer.Style = null; // remove white circle https://github.com/Mapsui/Mapsui/issues/760
            MapControl.Map.Layers.Add(GPSTrackerLayer);
            //GPSTrackerLayer.DataChanged += (a,b) => MapControl.RefreshData();

            MapMarkersLayer.IsMapInfoLayer = true;
            MapMarkersLayer.DataSource = new MapMarkerProvider(MapMarkersLayer);
            MapMarkersLayer.Style = null; // remove white circle https://github.com/Mapsui/Mapsui/issues/760
            MapControl.Map.Layers.Add(MapMarkersLayer);
            //MapMarkersLayer.DataChanged += (a, b) => MapControl.RefreshData();



            //LayerList.Initialize(MapControl.Map.Layers);
            //MapControl.ZoomToBox(new Point(0, 0), new Point(8192, 8192));
            //MapControl.Navigator.ZoomTo(1, new Point(512,512), 5);
        }

        //private void MapControlOnMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        //{
        //    GPSEditPopup.IsOpen = false;
        //    if (args.ClickCount > 1)
        //    {
        //
        //
        //        var info = MapControl.GetMapInfo(args.GetPosition(MapControl).ToMapsui(), 12);
        //
        //        if (info.Feature is GPSTrackerFeature gpsTrackerFeature)
        //        {
        //
        //            GPSEdit.Tracker = gpsTrackerFeature.Tracker;
        //            GPSEditPopup.Placement = PlacementMode.Mouse;
        //            GPSEditPopup.StaysOpen = false;
        //            GPSEditPopup.AllowsTransparency = true;
        //            GPSEditPopup.IsOpen = true;
        //        }
        //
        //        args.Handled = true;
        //    }
        //    else
        //    {
        //
        //    }
        //}


    }









    public class BoundBox : Mapsui.Geometries.Geometry
    {

        public BoundBox(BoundingBox x)
        {
            BoundingBox = x;
        }

        public override bool IsEmpty()
        {
            return false;
        }

        public override bool Equals(Geometry geom)
        {
            var point = geom as BoundBox;
            if (point == null) return false;
            return BoundingBox.Equals(point.BoundingBox);
        }

        public override BoundingBox BoundingBox { get; }

        public override double Distance(Mapsui.Geometries.Point point)
        {
            return BoundingBox.Distance(point);
        }

        public override bool Contains(Mapsui.Geometries.Point point)
        {
            return BoundingBox.Contains(point);
        }
    }


    public class SvgStyleRenderer : ISkiaStyleRenderer
    {
        public bool Draw(SKCanvas canvas, IReadOnlyViewport viewport, ILayer layer, IFeature feature, IStyle style,
            ISymbolCache symbolCache)
        {

            var image = ((SvgStyle)style).image;

            var center = viewport.Center;

            //SvgRenderer.Draw(canvas, image, -(float)viewport.Center.X, (float)viewport.Center.Y, (float)viewport.Rotation, 0,0,default, default, default, (float)viewport.Resolution);

            canvas.Save();


            var zoom = 1 / (float)viewport.Resolution;

            var canvasSize = canvas.LocalClipBounds;

            var canvasCenterX = canvasSize.Width / 2;
            var canvasCenterY = canvasSize.Height / 2;


            float width = (float)MapPage.currentBounds.Width;
            float height = (float)MapPage.currentBounds.Height;


            float num1 = (width / 2) * zoom;
            float num2 = (-height) * zoom;
            canvas.Translate(canvasCenterX, num2 + canvasCenterY);

            canvas.Translate(-(float)viewport.Center.X * zoom, (float)viewport.Center.Y * zoom);

            canvas.Scale(zoom, zoom);


            canvas.RotateDegrees((float)viewport.Rotation, 0.0f, 0.0f);


            canvas.DrawPicture(image.Picture, new SKPaint()
            {
                IsAntialias = true
            });
            canvas.Restore();






            return true;
        }
    }

    public class SvgStyle : VectorStyle
    {
        public Svg.Skia.SKSvg image; //#TODO https://github.com/wieslawsoltes/Svg.Skia
    }

    public class MarkerCache
    {
        public static MarkerCache Instance = new MarkerCache();

        private class MarkerRequest
        {
            public string path;
            public TaskCompletionSource<int> completionSource;
        }


        private Dictionary<string, MarkerRequest> requests = new Dictionary<string, MarkerRequest>(StringComparer.InvariantCultureIgnoreCase);

        public Task<int> GetBitmapId(ModuleMarker.MarkerType type, ModuleMarker.MarkerColor color)
        {
            lock (requests)
            {
                var path = $"{type.name}_{color.name}";

                if (requests.ContainsKey(path))
                    return requests[path].completionSource.Task;

                var request = new MarkerRequest { path = path, completionSource = new TaskCompletionSource<int>() };
                requests[path] = request;


                ImageDirectory.Instance.GetImage(type.icon)
                        .ContinueWith(
                            (x) =>
                            {

                                var image = x.Result.GetImage() as SKImage;
                                
                                var colorArr = color.color.Trim('[', ']').Split(',').Select(yx => float.Parse(yx)).ToList();

                                image = image.ApplyImageFilter(
                                    SkiaSharp.SKImageFilter.CreateColorFilter(
                                        SkiaSharp.SKColorFilter.CreateLighting(new SKColor((byte)(colorArr[0] * 255), (byte)(colorArr[1] * 255), (byte)(colorArr[2] * 255)), new SKColor(0, 0, 0))
                                    ),
                                    new SkiaSharp.SKRectI(0, 0, image.Width, image.Height),
                                    new SkiaSharp.SKRectI(0, 0, image.Width, image.Height),
                                    out var outSUbs,
                                    out SKPoint outoffs);

                                var content = new MemoryStream(image.Encode().ToArray());
                                int bitmapId;
                                lock (BitmapRegistry.Instance)
                                {
                                    bitmapId = BitmapRegistry.Instance.Register(content);
                                }

                                request.completionSource.SetResult(bitmapId);
                            });

                return request.completionSource.Task;
            }
        }


    }

    public class GPSTrackerFeature : Feature
    {
        public GPSTracker Tracker { get; private set; }
        private ImageStyle imgStyle;
        private LabelStyle lblStyle;
        public GPSTrackerFeature(GPSTracker tracker)
        {
            Tracker = tracker;
            this["Label"] = tracker.displayName;
            imgStyle = new ImageStyle
            {
                SymbolScale = 0.5,
                SymbolOffset = new Offset(0.0, 0, true),
                Fill = null,
                Outline = null,
                Line = null
            };

            var markerType = GameState.Instance.marker.markerTypes["hd_join"];
            var markerColor = GameState.Instance.marker.markerColors["ColorBlack"];

            MarkerCache.Instance.GetBitmapId(markerType, markerColor)
                .ContinueWith((bitmapId) => {
                    imgStyle.BitmapId = bitmapId.Result;
                });


            lblStyle = new Mapsui.Styles.LabelStyle { Text = tracker.displayName, BackColor = null, Offset = new Offset(1.2, 0, true), Halo = null };


            Styles.Add(imgStyle);
            Styles.Add(lblStyle);
            Geometry = new Mapsui.Geometries.Point(tracker.pos[0], tracker.pos[1]);
        }

        public void SetDisplayName(string newName)
        {
            this["Label"] = newName;
            lblStyle.Text = newName;
        }

        public void SetPosition(Mapsui.Geometries.Point newPos)
        {
            Geometry = newPos;
        }
    }

    public class GPSTrackerProvider : IProvider, IDisposable
    {
        public Layer GpsTrackerLayer { get; private set; }
        private BoundingBox _boundingBox = MapPage.currentBounds;

        public string CRS { get; set; }

        private Dictionary<string, IFeature> features = new Dictionary<string, IFeature>();


        public GPSTrackerProvider(Layer gpsTrackerLayer)
        {
            GpsTrackerLayer = gpsTrackerLayer;
            this.CRS = "";
            //this._boundingBox = MemoryProvider.GetExtents(this.Features);
            GameState.Instance.gps.trackers.CollectionChanged += (a, e) => OnTrackersUpdated();

            GameState.Instance.gps.PropertyChanged += (a, e) =>
            {
                if (e.PropertyName == nameof(ModuleGPS.trackers) && GameState.Instance.gps.trackers != null)
                {
                    GameState.Instance.gps.trackers.CollectionChanged += (b, c) => OnTrackersUpdated();
                    OnTrackersUpdated();
                }

            };



            //#TODO Also need EH on gps itself, I think the collection is initialized with a new one, without EH
            OnTrackersUpdated();
        }

        void OnDataChanged()
        {
            //GpsTrackerLayer.DataHasChanged();
            GpsTrackerLayer.RefreshData(GetExtents(), 1, ChangeType.Discrete);
        }
        private void OnTrackersUpdated()
        {
            foreach (var keyValuePair in GameState.Instance.gps.trackers)
            {
                if (!features.ContainsKey(keyValuePair.Key))
                {
                    var feature = new GPSTrackerFeature(keyValuePair.Value);

                    features[keyValuePair.Key] = feature;

                    keyValuePair.Value.PropertyChanged += (a, e) =>
                    {
                        if (e.PropertyName == nameof(GPSTracker.displayName))
                        {
                            feature.SetDisplayName(keyValuePair.Value.displayName);
                            OnDataChanged();
                        }
                        else if (e.PropertyName == nameof(GPSTracker.pos))
                        {
                            feature.SetPosition(new Mapsui.Geometries.Point(keyValuePair.Value.pos[0], keyValuePair.Value.pos[1]));
                            OnDataChanged();
                        }
                    };
                }
            }

            var toRemove = features.Where(x => !GameState.Instance.gps.trackers.ContainsKey(x.Key)).Select(x => x.Key)
                .ToList();

            toRemove.ForEach(x => features.Remove(x));

            OnDataChanged();
        }

        public virtual IEnumerable<IFeature> GetFeaturesInView(
          BoundingBox box,
          double resolution)
        {
            if (box == null)
                throw new ArgumentNullException(nameof(box));

            BoundingBox grownBox = box.Grow(resolution);

            return features.Values.Where(f => f.Geometry != null && f.Geometry.BoundingBox.Intersects(grownBox)).ToList();
        }

        public BoundingBox GetExtents()
        {
            return this._boundingBox;
        }

        private static BoundingBox GetExtents(IEnumerable<IFeature> features)
        {
            BoundingBox boundingBox = (BoundingBox)null;
            foreach (IFeature feature in features)
            {
                if (!feature.Geometry.IsEmpty())
                    boundingBox = boundingBox == null ? feature.Geometry.BoundingBox : boundingBox.Join(feature.Geometry.BoundingBox);
            }
            return boundingBox;
        }

        public void Dispose()
        {

        }
    }


    public class MapMarkerProvider : IProvider, IDisposable
    {
        public Layer MapMarkerLayer { get; private set; }
        private BoundingBox _boundingBox;

        public string CRS { get; set; }

        private Dictionary<string, IFeature> features = new Dictionary<string, IFeature>();


        public MapMarkerProvider(Layer mapMarkerLayer)
        {
            MapMarkerLayer = mapMarkerLayer;
            this.CRS = "";
            //this._boundingBox = MemoryProvider.GetExtents(this.Features);
            GameState.Instance.marker.markers.CollectionChanged += (a, e) => OnMarkersUpdated();

            GameState.Instance.marker.PropertyChanged += (a, e) => //#TODO probably don't need this anymore, same on GPSTracker
            {
                if (e.PropertyName == nameof(ModuleMarker.markers) && GameState.Instance.marker.markers != null)
                {
                    GameState.Instance.marker.markers.CollectionChanged += (b, c) => OnMarkersUpdated();
                    OnMarkersUpdated();
                }

            };


            OnMarkersUpdated();
        }

        private void OnMarkersUpdated()
        {
            foreach (var keyValuePair in GameState.Instance.marker.markers)
            {

                IFeature feature;

                if (!features.ContainsKey(keyValuePair.Key))
                {

                    var marker = keyValuePair.Value;
                    if (!GameState.Instance.marker.markerTypes.ContainsKey(marker.type)) continue;
                    var markerType = GameState.Instance.marker.markerTypes[marker.type];
                    if (!GameState.Instance.marker.markerColors.ContainsKey(marker.color)) continue;
                    var markerColor = GameState.Instance.marker.markerColors[marker.color];




                    feature = new Feature { ["Label"] = marker.text };

                    var symStyle = new SymbolStyle
                    {
                        SymbolScale = 0.5,
                        SymbolOffset = new Offset(0.0, 0, true),
                        Fill = null,
                        Outline = null,
                        Line = null,
                        SymbolType = SymbolType.Rectangle,
                        SymbolRotation = marker.dir

                    };

                    MarkerCache.Instance.GetBitmapId(markerType, markerColor)
                        .ContinueWith(
                            (bitmapId) =>
                            {
                                symStyle.BitmapId = bitmapId.Result;
                            });

                    feature.Styles.Add(symStyle);
                    feature.Styles.Add(new Mapsui.Styles.LabelStyle { Text = marker.text, BackColor = null, Offset = new Offset(1.2, 0, true), Halo = null });
                    feature.Geometry = new Mapsui.Geometries.Point(keyValuePair.Value.pos[0], keyValuePair.Value.pos[1]);
                    features[keyValuePair.Key] = feature;

                    keyValuePair.Value.PropertyChanged += (a, e) =>
                    {
                        if (e.PropertyName == nameof(ModuleMarker.ActiveMarker.text))
                        {
                            feature["Label"] = keyValuePair.Value.text;
                            //#TODO update Label style
                            foreach (var label in feature.Styles.Where(x => x is LabelStyle))
                                (label as LabelStyle).Text = keyValuePair.Value.text;

                            MapMarkerLayer.DataHasChanged();
                        }

                        else if (e.PropertyName == nameof(ModuleMarker.ActiveMarker.pos))
                        {
                            feature.Geometry = new Mapsui.Geometries.Point(keyValuePair.Value.pos[0], keyValuePair.Value.pos[1]);
                            MapMarkerLayer.DataHasChanged();
                        }
                        else if (e.PropertyName == nameof(ModuleMarker.ActiveMarker.dir))
                        {
                            foreach (var sym in feature.Styles.Where(x => x is SymbolStyle))
                                (sym as SymbolStyle).SymbolRotation = keyValuePair.Value.dir;
                            MapMarkerLayer.DataHasChanged();
                        }
                    };
                }
            }

            var toRemove = features.Where(x => !GameState.Instance.marker.markers.ContainsKey(x.Key)).Select(x => x.Key)
                .ToList();

            toRemove.ForEach(x => features.Remove(x));

            MapMarkerLayer.DataHasChanged();
        }

        public virtual IEnumerable<IFeature> GetFeaturesInView(
          BoundingBox box,
          double resolution)
        {
            if (box == null)
                throw new ArgumentNullException(nameof(box));

            BoundingBox grownBox = box.Grow(resolution);

            return features.Values.Where(f => f.Geometry != null && f.Geometry.BoundingBox.Intersects(grownBox)).ToList();
        }

        public BoundingBox GetExtents()
        {
            return this._boundingBox;
        }

        private static BoundingBox GetExtents(IReadOnlyList<IFeature> features)
        {
            BoundingBox boundingBox = (BoundingBox)null;
            foreach (IFeature feature in (IEnumerable<IFeature>)features)
            {
                if (!feature.Geometry.IsEmpty())
                    boundingBox = boundingBox == null ? feature.Geometry.BoundingBox : boundingBox.Join(feature.Geometry.BoundingBox);
            }
            return boundingBox;
        }

        public void Dispose()
        {

        }
    }






}