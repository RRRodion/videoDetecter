using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace videoDetecter
{
    public partial class Form1 : Form
    {
        private VideoCapture capture;
        private double frames;
        private double framesCounter;
        private bool play = false;
        private Bitmap previousFrame;
        private Bitmap currentFrame;
        private Bitmap processFrame;
        private double scaleRatio;

        public Form1()
        {
            InitializeComponent();
            timer1 = new Timer();
            timer1.Interval = 15;
            timer1.Tick += ProcessFrame;
            previousFrame = null;
            currentFrame = null;
            processFrame = null;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                DialogResult res = openFileDialog1.ShowDialog();

                if (res == DialogResult.OK)
                {
                    capture = new VideoCapture(openFileDialog1.FileName);
                    Mat m = new Mat();
                    capture.Read(m);
                    pictureBox1.Image = m.Bitmap;
                    frames = capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount);
                    framesCounter = 1;
                }
                else
                {
                    MessageBox.Show("No video selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ComputeFrameDifference()
        {
            Rectangle rect = new Rectangle(0, 0, processFrame.Width, processFrame.Height);
            BitmapData processFrameData = processFrame.LockBits(rect, ImageLockMode.ReadWrite, processFrame.PixelFormat);
            BitmapData previousFrameData = previousFrame.LockBits(rect, ImageLockMode.ReadOnly, previousFrame.PixelFormat);

            try
            {
                IntPtr processFramePtr = processFrameData.Scan0;
                IntPtr previousFramePtr = previousFrameData.Scan0;
                int bytesPerPixel = Image.GetPixelFormatSize(processFrame.PixelFormat) / 8;
                int stride = processFrameData.Stride;

                int width = processFrameData.Width;
                int height = processFrameData.Height;

                int processFrameBytes = stride * height;
                int previousFrameBytes = stride * height;
                byte[] processFrameBuffer = new byte[processFrameBytes];
                byte[] previousFrameBuffer = new byte[previousFrameBytes];

                Marshal.Copy(processFramePtr, processFrameBuffer, 0, processFrameBytes);
                Marshal.Copy(previousFramePtr, previousFrameBuffer, 0, previousFrameBytes);

                for (int y = 0; y < height; y++)
                {
                    int currentLine = y * stride;
                    int processFrameOffset = currentLine;
                    int previousFrameOffset = currentLine;

                    for (int x = 0; x < width; x++)
                    {
                        int processFrameIndex = processFrameOffset + x * bytesPerPixel;
                        int previousFrameIndex = previousFrameOffset + x * bytesPerPixel;

                        byte currentR = processFrameBuffer[processFrameIndex + 2];
                        byte currentG = processFrameBuffer[processFrameIndex + 1];
                        byte currentB = processFrameBuffer[processFrameIndex];

                        byte previousR = previousFrameBuffer[previousFrameIndex + 2];
                        byte previousG = previousFrameBuffer[previousFrameIndex + 1];
                        byte previousB = previousFrameBuffer[previousFrameIndex];

                        int diffR = Math.Abs(currentR - previousR);
                        int diffG = Math.Abs(currentG - previousG);
                        int diffB = Math.Abs(currentB - previousB);
                        int diffTotal = (diffR + diffG + diffB) / 3;

                        byte newColor = (diffTotal > 3) ? (byte)255 : (byte)0;
                        processFrameBuffer[processFrameIndex] = newColor;
                        processFrameBuffer[processFrameIndex + 1] = newColor;
                        processFrameBuffer[processFrameIndex + 2] = newColor;
                    }
                }

                Marshal.Copy(processFrameBuffer, 0, processFramePtr, processFrameBytes);
            }
            finally
            {
                processFrame.UnlockBits(processFrameData);
                previousFrame.UnlockBits(previousFrameData);
            }
        }


        private void ProcessFrame(object sender, EventArgs e)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (capture != null && capture.Ptr != IntPtr.Zero)
            {
                Mat m = new Mat();
                capture.Read(m);

                if (!m.IsEmpty)
                {
                    currentFrame = m.Bitmap;
                    processFrame = m.Bitmap;
                    currentFrame = ScaleBitmap(currentFrame);
                    processFrame = (Bitmap)currentFrame.Clone();

                    if (previousFrame == null)
                    {
                        previousFrame = (Bitmap)processFrame.Clone();
                    }
                    else
                    {
                        Bitmap temp = (Bitmap)processFrame.Clone();
                        ComputeFrameDifference();
                        ApplyMorphologicalOperations();
                        DrawMovingObjectContours(processFrame);
                        previousFrame.Dispose();
                        previousFrame = (Bitmap)temp.Clone();
                        temp.Dispose();
                    }

                    pictureBox1.Image = currentFrame;
                    //pictureBox1.Image = processFrame; ///// Вывод ч/б кадра
                    framesCounter++;

                    if (framesCounter > frames)
                    {
                        timer1.Stop();
                        capture.Dispose();
                        MessageBox.Show("Video processing completed.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    timer1.Stop();
                    capture.Dispose();
                    MessageBox.Show("Video processing interrupted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            Debug.WriteLine(stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (capture != null && capture.IsOpened)
            {
                play = !play;

                if (play)
                {
                    toolStripButton1.Text = "Pause";
                    timer1.Start();
                }
                else
                {
                    toolStripButton1.Text = "Play";
                    timer1.Stop();
                }
            }
            else
                MessageBox.Show("No video loaded!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (capture != null)
                capture.Dispose();
        }

        private void ApplyMorphologicalOperations()
        {
            CloseOperation();
            OpenOperation();
            ConvertToBinary();
        }

        private void CloseOperation()
        {
            int[,] structuringElement = new int[,]
            {
        { 1, 1, 1 },
        { 1, 1, 1 },
        { 1, 1, 1 }
            };

            Dilation(structuringElement);
            Erosion(structuringElement);
        }

        private void OpenOperation()
        {
            int[,] structuringElement = new int[,]
            {
        { 1, 1, 1 },
        { 1, 1, 1 },
        { 1, 1, 1 }
            };

            Erosion(structuringElement);
            Dilation(structuringElement);
        }

        private void Erosion(int[,] structuringElement)
        {
            Bitmap result = new Bitmap(processFrame.Width, processFrame.Height);
            Rectangle rect = new Rectangle(0, 0, processFrame.Width, processFrame.Height);
            BitmapData srcData = processFrame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int srcStride = srcData.Stride;
            int dstStride = dstData.Stride;
            IntPtr srcScan0 = srcData.Scan0;
            IntPtr dstScan0 = dstData.Scan0;

            byte[] structuringElementValues = new byte[9];

            for (int i = 0; i < 9; i++)
                structuringElementValues[i] = (byte)(structuringElement[i / 3, i % 3] * 255);

            unsafe
            {
                byte* srcPointer = (byte*)srcScan0;
                byte* dstPointer = (byte*)dstScan0;

                for (int y = 1; y < processFrame.Height - 1; y++)
                {
                    byte* srcRow = srcPointer + y * srcStride;
                    byte* dstRow = dstPointer + y * dstStride;

                    for (int x = 1; x < processFrame.Width - 1; x++)
                    {
                        bool shouldErode = true;

                        for (int i = -1; i <= 1; i++)
                        {
                            if (!shouldErode)
                                break;

                            for (int j = -1; j <= 1; j++)
                            {
                                byte* srcPixel = srcRow + (x + j) * 4;
                                byte srcPixelValue = (byte)((srcPixel[2] + srcPixel[1] + srcPixel[0]) / 3);

                                if (structuringElementValues[(i + 1) * 3 + (j + 1)] == 255 && srcPixelValue != 255)
                                {
                                    shouldErode = false;
                                    break;
                                }
                            }
                        }

                        byte* dstPixel = dstRow + x * 4;

                        if (shouldErode)
                        {
                            dstPixel[3] = 255;
                            dstPixel[2] = 255;
                            dstPixel[1] = 255;
                            dstPixel[0] = 255;
                        }
                        else
                        {
                            dstPixel[3] = 255;
                            dstPixel[2] = 0;
                            dstPixel[1] = 0;
                            dstPixel[0] = 0;
                        }
                    }
                }
            }

            processFrame.UnlockBits(srcData);
            result.UnlockBits(dstData);

            processFrame.Dispose();
            processFrame = result;
        }

        private void Dilation(int[,] structuringElement)
        {
            Bitmap result = new Bitmap(processFrame.Width, processFrame.Height);

            BitmapData srcData = processFrame.LockBits(new Rectangle(0, 0, processFrame.Width, processFrame.Height),
                ImageLockMode.ReadOnly, processFrame.PixelFormat);

            BitmapData destData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, result.PixelFormat);

            int structWidth = structuringElement.GetLength(1);
            int structHeight = structuringElement.GetLength(0);
            PixelFormat pixelFormat = processFrame.PixelFormat;
            int pixelSize = Image.GetPixelFormatSize(pixelFormat) / 8;
            IntPtr srcScan0 = srcData.Scan0;
            IntPtr destScan0 = destData.Scan0;
            int srcStride = srcData.Stride;
            int destStride = destData.Stride;

            int halfStructWidth = structWidth / 2;
            int halfStructHeight = structHeight / 2;

            for (int y = halfStructHeight; y < processFrame.Height - halfStructHeight; y++)
            {
                for (int x = halfStructWidth; x < processFrame.Width - halfStructWidth; x++)
                {
                    int maxR = 0;
                    int maxG = 0;
                    int maxB = 0;

                    for (int i = -halfStructHeight; i <= halfStructHeight; i++)
                    {
                        for (int j = -halfStructWidth; j <= halfStructWidth; j++)
                        {
                            int structElementValue = structuringElement[i + halfStructHeight, j + halfStructWidth];
                            int pixelX = x + j;
                            int pixelY = y + i;

                            IntPtr srcPixelAddress = srcScan0 + pixelY * srcStride + pixelX * pixelSize;
                            Color pixelColor = Color.FromArgb(Marshal.ReadInt32(srcPixelAddress));

                            if (structElementValue == 1)
                            {
                                maxR = Math.Max(maxR, pixelColor.R);
                                maxG = Math.Max(maxG, pixelColor.G);
                                maxB = Math.Max(maxB, pixelColor.B);
                            }
                        }
                    }

                    IntPtr destPixelAddress = destScan0 + y * destStride + x * pixelSize;
                    Marshal.WriteInt32(destPixelAddress, Color.FromArgb(maxR, maxG, maxB).ToArgb());
                }
            }

            processFrame.UnlockBits(srcData);
            result.UnlockBits(destData);

            processFrame = (Bitmap)result.Clone();
            result.Dispose();
        }

        private void ConvertToBinary()
        {
            Rectangle rect = new Rectangle(0, 0, processFrame.Width, processFrame.Height);
            BitmapData processFrameData = processFrame.LockBits(rect, ImageLockMode.ReadWrite, processFrame.PixelFormat);

            try
            {
                IntPtr processFramePtr = processFrameData.Scan0;
                int bytesPerPixel = Image.GetPixelFormatSize(processFrame.PixelFormat) / 8;
                int stride = processFrameData.Stride;
                int width = processFrameData.Width;
                int height = processFrameData.Height;

                int processFrameBytes = stride * height;
                byte[] processFrameBuffer = new byte[processFrameBytes];

                Marshal.Copy(processFramePtr, processFrameBuffer, 0, processFrameBytes);

                double brightnessThreshold = 0.6;

                for (int y = 0; y < height; y++)
                {
                    int currentLine = y * stride;
                    int processFrameOffset = currentLine;

                    for (int x = 0; x < width; x++)
                    {
                        int processFrameIndex = processFrameOffset + x * bytesPerPixel;

                        double brightness = CalculateBrightness(processFrameBuffer[processFrameIndex + 2], processFrameBuffer[processFrameIndex + 1], processFrameBuffer[processFrameIndex]);

                        byte newColor = (brightness > brightnessThreshold) ? (byte)255 : (byte)0;
                        processFrameBuffer[processFrameIndex] = newColor;
                        processFrameBuffer[processFrameIndex + 1] = newColor;
                        processFrameBuffer[processFrameIndex + 2] = newColor;
                    }
                }

                Marshal.Copy(processFrameBuffer, 0, processFramePtr, processFrameBytes);
            }
            finally
            {
                processFrame.UnlockBits(processFrameData);
            }
        }


        private double CalculateBrightness(byte r, byte g, byte b)
        {
            return (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        }

        private void DrawMovingObjectContours(Bitmap diffImage)
        {
            Graphics g = Graphics.FromImage(currentFrame);
            //Graphics g = Graphics.FromImage(processFrame); // отрисовка прямоугольников на ч/б кадре
            Pen redPen = new Pen(Color.Red, 2);
            List<List<Point>> contours = FindContours(diffImage);
            var minSize = 20;
            minSize = (int)(minSize * (2 - scaleRatio));

            foreach (List<Point> contour in contours)
            {
                Rectangle rect = GetBoundingBox(contour);

                if (rect.Width > minSize / 2 && rect.Height > minSize)
                    g.DrawRectangle(redPen, rect);
            }

            redPen.Dispose();
        }

        private List<List<Point>> FindContours(Bitmap image)
        {
            List<List<Point>> contours = new List<List<Point>>();
            bool[,] visited = new bool[image.Width, image.Height];

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (!visited[x, y] && IsObjectPixel(image.GetPixel(x, y)))
                    {
                        List<Point> contour = TraverseContour(image, visited, new Point(x, y));
                        contours.Add(contour);
                    }
                }
            }

            return contours;
        }

        private List<Point> TraverseContour(Bitmap image, bool[,] visited, Point startPoint)
        {
            List<Point> contour = new List<Point>();
            Stack<Point> stack = new Stack<Point>();
            stack.Push(startPoint);

            while (stack.Count > 0)
            {
                Point currentPoint = stack.Pop();

                if (IsValidPoint(image, visited, currentPoint))
                {
                    visited[currentPoint.X, currentPoint.Y] = true;
                    contour.Add(currentPoint);
                    List<Point> neighbors = GetNeighbors(currentPoint);

                    foreach (Point neighbor in neighbors)
                        stack.Push(neighbor);
                }
            }

            return contour;
        }

        private bool IsValidPoint(Bitmap image, bool[,] visited, Point point)
        {
            if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height)
                return false;

            if (visited[point.X, point.Y] || !IsObjectPixel(image.GetPixel(point.X, point.Y)))
                return false;

            return true;
        }

        private List<Point> GetNeighbors(Point point)
        {
            List<Point> neighbors = new List<Point>();
            neighbors.Add(new Point(point.X - 1, point.Y));
            neighbors.Add(new Point(point.X + 1, point.Y));
            neighbors.Add(new Point(point.X, point.Y - 1));
            neighbors.Add(new Point(point.X, point.Y + 1));

            return neighbors;
        }

        private bool IsObjectPixel(Color pixel)
        {
            return pixel.GetBrightness() > 0.5;
        }

        private Rectangle GetBoundingBox(List<Point> contour)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (Point point in contour)
            {
                if (point.X < minX)
                    minX = point.X;
                if (point.Y < minY)
                    minY = point.Y;
                if (point.X > maxX)
                    maxX = point.X;
                if (point.Y > maxY)
                    maxY = point.Y;
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            return new Rectangle(minX, minY, width, height);
        }

        private Bitmap ScaleBitmap(Bitmap sourceBitmap)
        {
            int targetWidth;
            int targetHeight;
            double scalePercentage;

            if (sourceBitmap.Width >= 1920 || sourceBitmap.Height >= 1080)
                scalePercentage = 0.2;
            else if (sourceBitmap.Width >= 1280 || sourceBitmap.Height >= 720)
                scalePercentage = 0.5;
            else
                scalePercentage = 1;

            scaleRatio = scalePercentage;
            targetWidth = (int)(sourceBitmap.Width * scalePercentage);
            targetHeight = (int)(sourceBitmap.Height * scalePercentage);
            Bitmap scaledBitmap = new Bitmap(targetWidth, targetHeight);

            using (Graphics graphics = Graphics.FromImage(scaledBitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                graphics.DrawImage(sourceBitmap, new Rectangle(0, 0, targetWidth, targetHeight));
            }

            return scaledBitmap;
        }
    }
}