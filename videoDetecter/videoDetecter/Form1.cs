using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

        private void ConvertToGrayscale()
        {
            Bitmap grayscaleImage = new Bitmap(processFrame.Width, processFrame.Height);

            int[,] kernel = new int[,]
            {
                { 1, 2, 1 },
                { 2, 4, 2 },
                { 1, 2, 1 }
            };

            int kernelSize = 3;
            int kernelWeight = 16;

            for (int y = 0; y < processFrame.Height; y++)
            {
                for (int x = 0; x < processFrame.Width; x++)
                {
                    int rTotal = 0, gTotal = 0, bTotal = 0;
                    int pixelCount = 0;

                    for (int i = -kernelSize / 2; i <= kernelSize / 2; i++)
                    {
                        for (int j = -kernelSize / 2; j <= kernelSize / 2; j++)
                        {
                            int offsetX = x + j;
                            int offsetY = y + i;

                            if (offsetX >= 0 && offsetX < processFrame.Width && offsetY >= 0 && offsetY < processFrame.Height)
                            {
                                Color pixel = processFrame.GetPixel(offsetX, offsetY);

                                rTotal += pixel.R * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                gTotal += pixel.G * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                bTotal += pixel.B * kernel[i + kernelSize / 2, j + kernelSize / 2];

                                pixelCount++;
                            }
                        }
                    }

                    int averageR = rTotal / kernelWeight;
                    int averageG = gTotal / kernelWeight;
                    int averageB = bTotal / kernelWeight;

                    Color grayscalePixel = Color.FromArgb(averageR, averageG, averageB);
                    grayscaleImage.SetPixel(x, y, grayscalePixel);
                }
            }

            processFrame = (Bitmap)grayscaleImage.Clone();
            grayscaleImage.Dispose();
        }

        private void ComputeFrameDifference()
        {

            for (int y = 0; y < processFrame.Height; y++)
            {
                for (int x = 0; x < processFrame.Width; x++)
                {
                    Color currentPixel = processFrame.GetPixel(x, y);
                    Color previousPixel = previousFrame.GetPixel(x, y);

                    int diffR = Math.Abs(currentPixel.R - previousPixel.R);
                    int diffG = Math.Abs(currentPixel.G - previousPixel.G);
                    int diffB = Math.Abs(currentPixel.B - previousPixel.B);

                    int diffTotal = (diffR + diffG + diffB) / 3;

                    if (diffTotal > 3)
                    {
                        processFrame.SetPixel(x, y, Color.White);
                    }
                    else
                    {
                        processFrame.SetPixel(x, y, Color.Black);
                    }
                }
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

                    ConvertToGrayscale();

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
            {
                MessageBox.Show("No video loaded!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (capture != null)
            {
                capture.Dispose();
            }
        }

        private void ApplyMorphologicalOperations()
        {
            int[,] structuringElement = new int[,]
            {
                { 1, 1, 1 },
                { 1, 1, 1 },
                { 1, 1, 1 }
            };

            Erosion(structuringElement);
            Dilation(structuringElement);
            ConvertToBinary();
        }

        private void Erosion(int[,] structuringElement)
        {
            Bitmap result = new Bitmap(processFrame.Width, processFrame.Height);

            for (int y = 1; y < processFrame.Height - 1; y++)
            {
                for (int x = 1; x < processFrame.Width - 1; x++)
                {
                    bool shouldErode = true;

                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (structuringElement[i + 1, j + 1] == 1 && processFrame.GetPixel(x + j, y + i).ToArgb() != Color.White.ToArgb())
                            {
                                shouldErode = false;
                                break;
                            }
                        }

                        if (!shouldErode)
                            break;
                    }

                    if (shouldErode)
                        result.SetPixel(x, y, Color.White);
                    else
                        result.SetPixel(x, y, Color.Black);
                }
            }
            processFrame = (Bitmap)result.Clone();
            result.Dispose();
        }

        private void Dilation(int[,] structuringElement)
        {
            Bitmap result = new Bitmap(processFrame.Width, processFrame.Height);

            for (int y = 1; y < processFrame.Height - 1; y++)
            {
                for (int x = 1; x < processFrame.Width - 1; x++)
                {
                    bool shouldDilate = false;

                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (structuringElement[i + 1, j + 1] == 1 && processFrame.GetPixel(x + j, y + i).ToArgb() == Color.White.ToArgb())
                            {
                                shouldDilate = true;
                                break;
                            }
                        }

                        if (shouldDilate)
                            break;
                    }

                    if (shouldDilate)
                        result.SetPixel(x, y, Color.White);
                    else
                        result.SetPixel(x, y, Color.Black);
                }
            }

            processFrame = (Bitmap)result.Clone();
            result.Dispose();
        }

        private void ConvertToBinary()
        {

            for (int y = 0; y < processFrame.Height; y++)
            {
                for (int x = 0; x < processFrame.Width; x++)
                {
                    Color pixel = processFrame.GetPixel(x, y);

                    if (pixel.GetBrightness() > 0.005)
                        processFrame.SetPixel(x, y, Color.White);
                    else
                        processFrame.SetPixel(x, y, Color.Black);
                }
            }
        }

        private void DrawMovingObjectContours(Bitmap diffImage)
        {
            Graphics g = Graphics.FromImage(currentFrame);
            Pen redPen = new Pen(Color.Red, 2);

            List<List<Point>> contours = FindContours(diffImage);

            foreach (List<Point> contour in contours)
            {
                Rectangle rect = GetBoundingBox(contour);

                if (rect.Width > 20 && rect.Height > 20)
                {
                    g.DrawRectangle(redPen, rect);
                }
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
                    {
                        stack.Push(neighbor);
                    }
                }
            }

            return contour;
        }

        private bool IsValidPoint(Bitmap image, bool[,] visited, Point point)
        {
            if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height)
            {
                return false;
            }

            if (visited[point.X, point.Y] || !IsObjectPixel(image.GetPixel(point.X, point.Y)))
            {
                return false;
            }

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
    }
}