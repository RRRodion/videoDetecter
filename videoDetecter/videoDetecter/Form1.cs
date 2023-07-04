using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace videoDetecter
{
    public partial class Form1 : Form
    {
        private VideoCapture capture;
        private double frames;
        private double framesCounter;
        private double fps;
        private bool play = false;
        private Bitmap previousFrame;

        public Form1()
        {
            InitializeComponent();
            timer1 = new Timer();
            timer1.Interval = 15; // Set the timer interval based on the video frame rate
            timer1.Tick += ProcessFrame;
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
                    fps = capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
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

        private Bitmap ConvertToGrayscale(Bitmap image)
        {
            Bitmap grayscaleImage = new Bitmap(image.Width, image.Height);

            // Gaussian kernel
            int[,] kernel = new int[,]
            {
{ 1, 2, 1 },
{ 2, 4, 2 },
{ 1, 2, 1 }
            };

            int kernelSize = 3;
            int kernelWeight = 16;

            // Convolution operation
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int rTotal = 0, gTotal = 0, bTotal = 0;
                    int pixelCount = 0;

                    // Apply the kernel to the neighborhood pixels
                    for (int i = -kernelSize / 2; i <= kernelSize / 2; i++)
                    {
                        for (int j = -kernelSize / 2; j <= kernelSize / 2; j++)
                        {
                            int offsetX = x + j;
                            int offsetY = y + i;

                            // Check if the pixel is within the image boundaries
                            if (offsetX >= 0 && offsetX < image.Width && offsetY >= 0 && offsetY < image.Height)
                            {
                                Color pixel = image.GetPixel(offsetX, offsetY);

                                // Apply the kernel to each channel (R, G, B)
                                rTotal += pixel.R * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                gTotal += pixel.G * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                bTotal += pixel.B * kernel[i + kernelSize / 2, j + kernelSize / 2];

                                pixelCount++;
                            }
                        }
                    }

                    // Normalize the color values
                    int averageR = rTotal / kernelWeight;
                    int averageG = gTotal / kernelWeight;
                    int averageB = bTotal / kernelWeight;

                    Color grayscalePixel = Color.FromArgb(averageR, averageG, averageB);
                    grayscaleImage.SetPixel(x, y, grayscalePixel);
                }
            }

            return grayscaleImage;
        }

        private Bitmap ComputeFrameDifference(Bitmap currentFrame, Bitmap previousFrame)
        {
            Bitmap diffImage = new Bitmap(currentFrame.Width, currentFrame.Height);

            for (int y = 0; y < currentFrame.Height; y++)
            {
                for (int x = 0; x < currentFrame.Width; x++)
                {
                    Color currentPixel = currentFrame.GetPixel(x, y);
                    Color previousPixel = previousFrame.GetPixel(x, y);

                    int diffR = Math.Abs(currentPixel.R - previousPixel.R);
                    int diffG = Math.Abs(currentPixel.G - previousPixel.G);
                    int diffB = Math.Abs(currentPixel.B - previousPixel.B);

                    int diffTotal = (diffR + diffG + diffB) / 3;

                    if (diffTotal > 3)
                    {
                        diffImage.SetPixel(x, y, Color.White);
                    }
                    else
                    {
                        diffImage.SetPixel(x, y, Color.Black);
                    }
                }
            }

            return diffImage;
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            if (capture != null && capture.Ptr != IntPtr.Zero)
            {
                Mat m = new Mat();
                capture.Read(m);

                if (!m.IsEmpty)
                {
                    // Convert current frame to bitmap
                    Bitmap currentFrame = m.Bitmap;

                    // Convert current frame to grayscale
                    Bitmap grayscaleImage = ConvertToGrayscale(currentFrame);

                    if (previousFrame != null)
                    {
                        // Compute frame difference
                        Bitmap diffImage = ComputeFrameDifference(grayscaleImage, previousFrame);

                        Bitmap processedImage = ApplyMorphologicalOperations(diffImage);

                        pictureBox2.Image = processedImage;
                        // Track motion on the difference image
                        DrawMovingObjectContours(processedImage, currentFrame);
                        // Dispose the previous frame
                        previousFrame.Dispose();
                    }

                    // Update previous frame
                    previousFrame = grayscaleImage;

                    // Display the current frame
                    pictureBox1.Image = currentFrame;

                    // Update frames counter
                    framesCounter++;

                    // Check if all frames have been processed
                    if (framesCounter > frames)
                    {
                        // Stop the timer
                        timer1.Stop();

                        // Release the video capture
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

        private Bitmap ApplyMorphologicalOperations(Bitmap image)
        {
            Bitmap processedImage = new Bitmap(image.Width, image.Height);

            // Define the structuring element for morphological operations (3x3 square)
            int[,] structuringElement = new int[,]
            {
        { 1, 1, 1 },
        { 1, 1, 1 },
        { 1, 1, 1 }
            };

            // Apply erosion operation
            Bitmap erodedImage = Erosion(image, structuringElement);

            // Apply opening operation (erosion followed by dilation)
            Bitmap openedImage = Dilation(erodedImage, structuringElement);

            // Convert the opened image to binary (black and white)
            Bitmap binaryImage = ConvertToBinary(openedImage);

            return binaryImage;
        }

        private Bitmap Erosion(Bitmap image, int[,] structuringElement)
        {
            Bitmap result = new Bitmap(image.Width, image.Height);

            for (int y = 1; y < image.Height - 1; y++)
            {
                for (int x = 1; x < image.Width - 1; x++)
                {
                    bool shouldErode = true;

                    // Check if all structuring element pixels are white
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (structuringElement[i + 1, j + 1] == 1 && image.GetPixel(x + j, y + i).ToArgb() != Color.White.ToArgb())
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

            return result;
        }

        private Bitmap Dilation(Bitmap image, int[,] structuringElement)
        {
            Bitmap result = new Bitmap(image.Width, image.Height);

            for (int y = 1; y < image.Height - 1; y++)
            {
                for (int x = 1; x < image.Width - 1; x++)
                {
                    bool shouldDilate = false;

                    // Check if any structuring element pixel overlaps with a white pixel in the image
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (structuringElement[i + 1, j + 1] == 1 && image.GetPixel(x + j, y + i).ToArgb() == Color.White.ToArgb())
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

            return result;
        }

        private Bitmap ConvertToBinary(Bitmap image)
        {
            Bitmap binaryImage = new Bitmap(image.Width, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    if (pixel.GetBrightness() > 0.005)
                        binaryImage.SetPixel(x, y, Color.White);
                    else
                        binaryImage.SetPixel(x, y, Color.Black);
                }
            }

            return binaryImage;
        }

        private void DrawMovingObjectContours(Bitmap diffImage, Bitmap currentFrame)
        {
            Graphics g = Graphics.FromImage(currentFrame);
            Pen redPen = new Pen(Color.Red, 2);

            // Find contours in the difference image
            List<List<Point>> contours = FindContours(diffImage);

            // Draw bounding rectangles around large contours
            foreach (List<Point> contour in contours)
            {
                Rectangle rect = GetBoundingBox(contour);

                // Check if the contour is large enough
                if (rect.Width > 20 && rect.Height > 20)
                {
                    // Draw a red rectangle around the contour
                    g.DrawRectangle(redPen, rect);
                }
            }

            //g.Dispose();
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
            // Define the condition for considering a pixel as part of the moving object.
            // This condition can be based on the pixel intensity, color, or other features.
            // Modify this method according to your requirements.
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