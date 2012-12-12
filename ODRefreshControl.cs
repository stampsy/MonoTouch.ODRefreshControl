using System;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using MonoTouch.CoreFoundation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

public class ODRefreshControl : UIControl
{
    const float TotalViewHeight = 400;
    const float OpenedViewHeight = 44;
    const float MinTopPadding = 9;
    const float MaxTopPadding = 5;
    const float MinTopRadius = 12.5f;
    const float MaxTopRadius = 16;
    const float MinBottomRadius = 3;
    const float MaxBottomRadius = 16;
    const float MinBottomPadding = 4;
    const float MaxBottomPadding = 6;
    const float MinArrowSize = 2;
    const float MaxArrowSize = 3;
    const float MinArrowRadius = 5;
    const float MaxArrowRadius = 7;
    const float MaxDistance = 53;

    CAShapeLayer _shapeLayer;
    CAShapeLayer _arrowLayer;
    CAShapeLayer _highlightLayer;
    UIView _activity;
    bool _refreshing;
    bool _canRefresh;
    bool _ignoreInset;
    bool _ignoreOffset;
    bool _didSetInset;
    bool _hasSectionHeaders;
    float _lastOffset;
    UIColor _tintColor;


    public UIScrollView ScrollView { get; private set; }
    public UIEdgeInsets OriginalContentInset { get; private set; }
    
    public bool IsRefreshing {
        get { return _refreshing; }
    }
    
    public override bool Enabled {
        get { return base.Enabled; }
        set {
            base.Enabled = value;
            _shapeLayer.Hidden = !value;
        }
    }

    public UIColor TintColor {
        get { return _tintColor; }
        set {
            _tintColor = value;
            _shapeLayer.FillColor = value.CGColor;
        }
    }
    
    public UIActivityIndicatorViewStyle ActivityIndicatorViewStyle {
        get {
            if (_activity is UIActivityIndicatorView)
                return ((UIActivityIndicatorView) _activity).ActivityIndicatorViewStyle;
            
            return UIActivityIndicatorViewStyle.Gray;
        }
    }
    
    public UIColor ActivityIndicatorViewColor {
        get {
            if (_activity is UIActivityIndicatorView)
                return ((UIActivityIndicatorView) _activity).Color;
            
            return null;
        }
        set {
            if (_activity is UIActivityIndicatorView)
                ((UIActivityIndicatorView) _activity).Color = value;
        }
    }

    public ODRefreshControl (UIScrollView scrollView, UIView activity)
        : base (new RectangleF (0, (-TotalViewHeight + scrollView.ContentInset.Top), scrollView.Frame.Width, TotalViewHeight))
    {
        ScrollView = scrollView;
        OriginalContentInset = scrollView.ContentInset;

        AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
        ScrollView.AddSubview (this);
        ScrollView.AddObserver (this, new NSString ("contentOffset"), NSKeyValueObservingOptions.New, IntPtr.Zero);
        ScrollView.AddObserver (this, new NSString ("contentInset"), NSKeyValueObservingOptions.New, IntPtr.Zero);

        _activity = activity ?? new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
        _activity.Center = new PointF ((float) Math.Floor (Frame.Size.Width / 2), (float) Math.Floor (Frame.Size.Height / 2));
        _activity.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
        _activity.Alpha = 1;

        if (_activity is UIActivityIndicatorView) {
            ((UIActivityIndicatorView) _activity).StartAnimating ();
        }
        AddSubview (_activity);

        _refreshing = false;
        _canRefresh = true;

        _ignoreInset = false;
        _ignoreOffset = false;
        _didSetInset = false;
        _hasSectionHeaders = false;
        _tintColor = UIColor.FromRGB (155, 162, 172);

        _shapeLayer = new CAShapeLayer {
            FillColor = _tintColor.CGColor,
            StrokeColor = UIColor.DarkGray.ColorWithAlpha (.5f).CGColor,
            LineWidth = .5f,
            ShadowColor = UIColor.Black.CGColor,
            ShadowOffset = new SizeF (0, 1),
            ShadowOpacity = .4f,
            ShadowRadius = .5f
        };

        Layer.AddSublayer (_shapeLayer);
        
        _arrowLayer = new CAShapeLayer {
            StrokeColor = UIColor.DarkGray.ColorWithAlpha (.5f).CGColor,
            LineWidth = .5f,
            FillColor = UIColor.White.CGColor
        };

        _shapeLayer.AddSublayer (_arrowLayer);

        _highlightLayer = new CAShapeLayer {
            FillColor = UIColor.White.ColorWithAlpha (.2f).CGColor
        };

        _shapeLayer.AddSublayer (_highlightLayer);
    }

    public ODRefreshControl (UIScrollView scrollView) : this (scrollView, null) { }

    public override void ObserveValue (NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
    {
        if (keyPath == "contentInset") {
            if (!_ignoreInset) {
                OriginalContentInset = ((NSValue) change ["new"]).UIEdgeInsetsValue;
                Frame = new RectangleF (0, - (TotalViewHeight + ScrollView.ContentInset.Top), ScrollView.Frame.Size.Width, TotalViewHeight);
            }
            return;
        }

        if (!Enabled || _ignoreOffset)
            return;

        var offset = ((NSValue) change ["new"]).PointFValue.Y + OriginalContentInset.Top;

        if (_refreshing) {
            if (offset != 0) {
                // Keep thing pinned at the top

                CATransaction.Begin ();
                CATransaction.DisableActions = true;
                _shapeLayer.Position = new PointF (0, MaxDistance + offset + OpenedViewHeight);
                CATransaction.Commit ();

                _activity.Center = new PointF ((float) Math.Floor (Frame.Size.Width / 2),
                    Math.Min (
                        offset + Frame.Height + (float) Math.Floor (OpenedViewHeight / 2),
                        Frame.Height - OpenedViewHeight / 2    
                    )
                );

                _ignoreInset = true;
                _ignoreOffset = true;

                if (offset < 0) {
                    // Set the inset depending on the situation
                    if (offset >= -OpenedViewHeight) {
                        if (!ScrollView.Dragging) {
                            if (!_didSetInset) {
                                _didSetInset = true;
                                _hasSectionHeaders = false;

                                if (ScrollView is UITableView) {
                                    var tableView = (UITableView) ScrollView;
                                    for (var i = 0; i < tableView.NumberOfSections (); i++) {
                                        if (tableView.RectForHeaderInSection (i).Size.Height > 0) {
                                            _hasSectionHeaders = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (_hasSectionHeaders) {
                                ScrollView.ContentInset = new UIEdgeInsets (Math.Min (-offset, OpenedViewHeight) + OriginalContentInset.Top, OriginalContentInset.Left, OriginalContentInset.Bottom, OriginalContentInset.Right);
                            } else {
                                ScrollView.ContentInset = new UIEdgeInsets (OpenedViewHeight + OriginalContentInset.Top, OriginalContentInset.Left, OriginalContentInset.Bottom, OriginalContentInset.Right);
                            }
                        } else if (_didSetInset && _hasSectionHeaders) {
                            ScrollView.ContentInset = new UIEdgeInsets (-offset + OriginalContentInset.Top, OriginalContentInset.Left, OriginalContentInset.Bottom, OriginalContentInset.Right);
                        } 
                    }
                } else if (_hasSectionHeaders) {
                    ScrollView.ContentInset = OriginalContentInset;
                }

                _ignoreInset = false;
                _ignoreOffset = false;
            }

            return;
        } else {
            // Check if we can trigger a new refresh and if we can draw the control
            bool dontDraw = false;

            if (!_canRefresh) {
                if (offset >= 0) {
                    // We can refresh again after the control is scrolled out of view
                    _canRefresh = true;
                    _didSetInset = false;
                } else {
                    dontDraw = true;
                }
            } else {
                if (offset >= 0) {
                    // Don't draw if the control is not visible
                    dontDraw = true;
                }
            }

            if (offset > 0 && _lastOffset > offset && !ScrollView.Tracking) {
                // If we are scrolling too fast, don't draw, and don't trigger unless the scrollView bounced back
                _canRefresh = false;
                dontDraw = true;
            }

            if (dontDraw) {
                _shapeLayer.Path = null;
                _shapeLayer.ShadowPath = new CGPath (IntPtr.Zero);
                _arrowLayer.Path = null;
                _highlightLayer.Path = null;
                _lastOffset = offset;
                return;
            }
        }

        _lastOffset = offset;
        bool triggered = false;

        CGPath path = new CGPath ();

        //Calculate some useful points and values
        var verticalShift = Math.Max (0, -((MaxTopRadius + MaxBottomRadius + MaxTopPadding + MaxBottomPadding) + offset));
        var distance = Math.Min (MaxDistance, Math.Abs (verticalShift));
        var percentage = 1 - (distance / MaxDistance);

        float currentTopPadding = lerp (MinTopPadding, MaxTopPadding, percentage);
        float currentTopRadius = lerp (MinTopRadius, MaxTopRadius, percentage);
        float currentBottomRadius = lerp (MinBottomRadius, MaxBottomRadius, percentage);
        float currentBottomPadding = lerp (MinBottomPadding, MaxBottomPadding, percentage);
        
        var bottomOrigin = new PointF ((float) Math.Floor (Frame.Size.Width / 2), Bounds.Size.Height - currentBottomPadding - currentBottomRadius);
        var topOrigin = PointF.Empty;

        if (distance == 0) {
            topOrigin = new PointF ((float) Math.Floor (Bounds.Size.Width / 2), bottomOrigin.Y);
        } else {
            topOrigin = new PointF ((float) Math.Floor (Bounds.Size.Width / 2), Bounds.Size.Height + offset + currentTopPadding + currentTopRadius);

            if (percentage == 0) {
                bottomOrigin.Y -= ((Math.Abs (verticalShift) - MaxDistance));
                triggered = true;
            }
        }

        //Top semicircle
        path.AddArc (topOrigin.X, topOrigin.Y, currentTopRadius, 0, (float) Math.PI, true);

        //Left curve
        var leftCp1 = new PointF (lerp ((topOrigin.X - currentTopRadius), (bottomOrigin.X - currentBottomRadius), 0.1f), lerp (topOrigin.Y, bottomOrigin.Y, .2f));
        var leftCp2 = new PointF (lerp ((topOrigin.X - currentTopRadius), (bottomOrigin.X - currentBottomRadius), 0.9f), lerp (topOrigin.Y, bottomOrigin.Y, .2f));
        var leftDestination = new PointF (bottomOrigin.X - currentBottomRadius, bottomOrigin.Y);

        path.AddCurveToPoint (leftCp1, leftCp2, leftDestination);

        //Bottom semicircle
        path.AddArc (bottomOrigin.X, bottomOrigin.Y, currentBottomRadius, (float) Math.PI, 0, true);

        //Right curve
        var rightCp2 = new PointF (lerp ((topOrigin.X + currentTopRadius), (bottomOrigin.X + currentBottomRadius), 0.1f), lerp (topOrigin.Y, bottomOrigin.Y, .2f));
        var rightCp1 = new PointF (lerp ((topOrigin.X + currentTopRadius), (bottomOrigin.X + currentBottomRadius), 0.9f), lerp (topOrigin.Y, bottomOrigin.Y, .2f));
        var rightDestination = new PointF (bottomOrigin.X + currentTopRadius, topOrigin.Y);

        path.AddCurveToPoint (rightCp1, rightCp2, rightDestination);
        path.CloseSubpath ();

        if (!triggered) {
            // Set paths

            _shapeLayer.Path = path;
            _shapeLayer.ShadowPath = path;

            // Add the arrow shape

            var currentArrowSize = lerp (MinArrowSize, MaxArrowSize, percentage);
            var currentArrowRadius = lerp (MinArrowRadius, MaxArrowRadius, percentage);

            var arrowBigRadius = currentArrowRadius + (currentArrowSize / 2);
            var arrowSmallRadius = currentArrowRadius - (currentArrowSize / 2);

            var arrowPath = new CGPath ();
            arrowPath.AddArc (topOrigin.X, topOrigin.Y, arrowBigRadius, 0, 3 * (float) Math.PI / 2.0f, false);
            arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius - currentArrowSize);
            arrowPath.AddLineToPoint (topOrigin.X + (2 * currentArrowSize), topOrigin.Y - arrowBigRadius + (currentArrowSize / 2));
            arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius + (2 * currentArrowSize));
            arrowPath.AddLineToPoint (topOrigin.X, topOrigin.Y - arrowBigRadius + currentArrowSize);
            arrowPath.AddArc (topOrigin.X, topOrigin.Y, arrowSmallRadius, 3 * (float) Math.PI / 2.0f, 0, true);
            arrowPath.CloseSubpath ();

            _arrowLayer.Path = arrowPath;
            _arrowLayer.FillRule = CAShapeLayer.FillRuleEvenOdd;
            arrowPath.Dispose ();

            // Add the highlight shape

            var highlightPath = new CGPath ();
			highlightPath.AddArc (topOrigin.X, topOrigin.Y, currentTopRadius, 0, (float) Math.PI, true);
			highlightPath.AddArc (topOrigin.X, topOrigin.Y + 1.25f, currentTopRadius, (float) Math.PI, 0, false);

            _highlightLayer.Path = highlightPath;
            _highlightLayer.FillRule = CAShapeLayer.FillRuleNonZero;
            highlightPath.Dispose ();
        } else {
            // Start the shape disappearance animation

            var radius = lerp (MinBottomRadius, MaxBottomRadius, .2f);
            var pathMorph = CABasicAnimation.FromKeyPath ("path");

            pathMorph.Duration = .15f;
            pathMorph.FillMode = CAFillMode.Forwards;
            pathMorph.RemovedOnCompletion = false;

            var toPath = new CGPath ();
            toPath.AddArc (topOrigin.X, topOrigin.Y, radius, 0, (float)Math.PI, true);
            toPath.AddCurveToPoint (topOrigin.X - radius, topOrigin.Y, topOrigin.X - radius, topOrigin.Y, topOrigin.X - radius, topOrigin.Y);
            toPath.AddArc (topOrigin.X, topOrigin.Y, radius, (float) Math.PI, 0, true);
            toPath.AddCurveToPoint (topOrigin.X + radius, topOrigin.Y, topOrigin.X + radius, topOrigin.Y, topOrigin.X + radius, topOrigin.Y);
            toPath.CloseSubpath ();

            pathMorph.To = new NSValue (toPath.Handle);
            _shapeLayer.AddAnimation (pathMorph, null);

            var shadowPathMorph = CABasicAnimation.FromKeyPath ("shadowPath");
            shadowPathMorph.Duration = .15f;
            shadowPathMorph.FillMode = CAFillMode.Forwards;
            shadowPathMorph.RemovedOnCompletion = false;
            shadowPathMorph.To = new NSValue (toPath.Handle);

            _shapeLayer.AddAnimation (shadowPathMorph, null);
            toPath.Dispose ();

            var shapeAlphaAnimation = CABasicAnimation.FromKeyPath ("opacity");
            shapeAlphaAnimation.Duration = .1f;
            shapeAlphaAnimation.BeginTime = CABasicAnimation.CurrentMediaTime ();
            shapeAlphaAnimation.To = new NSNumber (0);
            shapeAlphaAnimation.FillMode = CAFillMode.Forwards;
            shapeAlphaAnimation.RemovedOnCompletion = false;
            _shapeLayer.AddAnimation (shapeAlphaAnimation, null);

            var alphaAnimation = CABasicAnimation.FromKeyPath ("opacity");
            alphaAnimation.Duration = .1f;
            alphaAnimation.To = new NSNumber (0);
            alphaAnimation.FillMode = CAFillMode.Forwards;
            alphaAnimation.RemovedOnCompletion = false;

            _arrowLayer.AddAnimation (alphaAnimation, null);
            _highlightLayer.AddAnimation (alphaAnimation, null);

            CATransaction.Begin ();
            CATransaction.DisableActions = true;
            _activity.Layer.Transform = CATransform3D.MakeScale (.1f, .1f, 1);
            CATransaction.Commit ();

            UIView.Animate (.2f, .15f, UIViewAnimationOptions.CurveLinear, () => {
                _activity.Alpha = 1;
                _activity.Layer.Transform = CATransform3D.MakeScale (1, 1, 1);
            }, null);

            _refreshing = true;
            _canRefresh = false;

            SendActionForControlEvents (UIControlEvent.ValueChanged);
        }

        path.Dispose ();
    }

    public void BeginRefreshing ()
    {
        if (!_refreshing) {
            var alphaAnimation = CABasicAnimation.FromKeyPath("opacity");

            alphaAnimation.Duration = 0.0001f;
            alphaAnimation.To = new NSNumber (0);
            alphaAnimation.FillMode = CAFillMode.Forwards;
            alphaAnimation.RemovedOnCompletion = false;
            _shapeLayer.AddAnimation (alphaAnimation, null);
            _arrowLayer.AddAnimation (alphaAnimation, null);
            _highlightLayer.AddAnimation (alphaAnimation, null);
            
            _activity.Alpha = 1;
            _activity.Layer.Transform = CATransform3D.MakeScale (1, 1, 1);
            
            var offset = ScrollView.ContentOffset;
            _ignoreInset = true;

            ScrollView.ContentInset = new UIEdgeInsets (OpenedViewHeight + OriginalContentInset.Top, OriginalContentInset.Left, OriginalContentInset.Bottom, OriginalContentInset.Right);
            _ignoreInset = false;
            ScrollView.SetContentOffset (offset, false);

            _refreshing = false;
            _canRefresh = false;
        }
    }

    public void EndRefreshing ()
    {
        if (_refreshing) {
            _refreshing = false;

            UIView.Animate (.4, () => {
                _ignoreInset = true;
                ScrollView.ContentInset = OriginalContentInset;
                _ignoreInset = false;
                _activity.Alpha = 0;
                _activity.Layer.Transform = CATransform3D.MakeScale (.1f, .1f, 1);
            }, () => {
                _shapeLayer.RemoveAllAnimations ();
                _shapeLayer.Path = null;
                _shapeLayer.ShadowPath = new CGPath (IntPtr.Zero);
                _shapeLayer.Position = PointF.Empty;

                _arrowLayer.RemoveAllAnimations ();
                _arrowLayer.Path = null;

                _highlightLayer.RemoveAllAnimations ();
                _highlightLayer.Path = null;

                _ignoreInset = true;
                ScrollView.ContentInset = OriginalContentInset;
                _ignoreInset = false;
            });
        }
    }

    
    public override void WillMoveToSuperview (UIView newSuperview)
    {
        base.WillMoveToSuperview (newSuperview);
        if (newSuperview == null) {
            ScrollView.RemoveObserver (this, new NSString ("contentOffset"));
            ScrollView.RemoveObserver (this, new NSString ("contentInset"));
            ScrollView = null;
        }
    }

    protected override void Dispose (bool disposing)
    {
        if (disposing) {
            ScrollView.RemoveObserver (this, new NSString ("contentOffset"));
            ScrollView.RemoveObserver (this, new NSString ("contentInset"));
            ScrollView = null;
        }

        base.Dispose (disposing);
    }

    static float lerp (float a, float b, float p)
    {
        return a + (b - a) * p;
    }
}
