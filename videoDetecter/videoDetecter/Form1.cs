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

        private void ConvertToGrayscale()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Bitmap grayscaleImage = new Bitmap(processFrame.Width, processFrame.Height);

            int[,] kernel = new int[,]
            {
        { 1, 2, 1 },
        { 2, 4, 2 },
        { 1, 2, 1 }
            };

            int kernelSize = 3;
            int kernelWeight = 16;


            // Вычислить сумму всех элементов ядра заранее
            int kernelSum = 0;
            for (int i = 0; i < kernelSize; i++)
            {
                for (int j = 0; j < kernelSize; j++)
                {
                    kernelSum += kernel[i, j];
                }
            }

            BitmapData processFrameData = processFrame.LockBits(new Rectangle(0, 0, processFrame.Width, processFrame.Height), ImageLockMode.ReadOnly, processFrame.PixelFormat);
            BitmapData grayscaleImageData = grayscaleImage.LockBits(new Rectangle(0, 0, grayscaleImage.Width, grayscaleImage.Height), ImageLockMode.WriteOnly, grayscaleImage.PixelFormat);

            try
            {
                int bytesPerPixel = Bitmap.GetPixelFormatSize(processFrame.PixelFormat) / 8;

                unsafe
                {
                    byte* processFramePtr = (byte*)processFrameData.Scan0;
                    byte* grayscaleImagePtr = (byte*)grayscaleImageData.Scan0;

                    for (int y = 0; y < processFrame.Height; y++)
                    {
                        for (int x = 0; x < processFrame.Width; x++)
                        {
                            int rTotal = 0, gTotal = 0, bTotal = 0;
                            int pixelCount = 0;

                            for (int i = -kernelSize / 2; i <= kernelSize / 2; i++)
                            {
                                int offsetY = y + i;

                                if (offsetY >= 0 && offsetY < processFrame.Height)
                                {
                                    for (int j = -kernelSize / 2; j <= kernelSize / 2; j++)
                                    {
                                        int offsetX = x + j;

                                        if (offsetX >= 0 && offsetX < processFrame.Width)
                                        {
                                            byte* pixelPtr = processFramePtr + offsetY * processFrameData.Stride + offsetX * bytesPerPixel;

                                            byte b = pixelPtr[0];
                                            byte g = pixelPtr[1];
                                            byte r = pixelPtr[2];

                                            rTotal += r * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                            gTotal += g * kernel[i + kernelSize / 2, j + kernelSize / 2];
                                            bTotal += b * kernel[i + kernelSize / 2, j + kernelSize / 2];

                                            pixelCount++;
                                        }
                                    }
                                }
                            }

                            byte* grayscalePixelPtr = grayscaleImagePtr + y * grayscaleImageData.Stride + x * bytesPerPixel;

                            grayscalePixelPtr[0] = (byte)(bTotal / kernelSum);
                            grayscalePixelPtr[1] = (byte)(gTotal / kernelSum);
                            grayscalePixelPtr[2] = (byte)(rTotal / kernelSum);
                            if (bytesPerPixel == 4)
                            {
                                grayscalePixelPtr[3] = 255;
                            }
                        }
                    }
                }
            }
            finally
            {
                processFrame.UnlockBits(processFrameData);
                grayscaleImage.UnlockBits(grayscaleImageData);
            }

            processFrame = (Bitmap)grayscaleImage.Clone();
            grayscaleImage.Dispose();

            Debug.WriteLine("Grayscale - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
        }


        private void ComputeFrameDifference()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

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

                    if (diffTotal > 2)
                    {
                        processFrame.SetPixel(x, y, Color.White);
                    }
                    else
                    {
                        processFrame.SetPixel(x, y, Color.Black);
                    }
                }
            }
            Debug.WriteLine("FrameDiff - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Применение операции морфологического закрытия для заполнения областей объекта
            CloseOperation();

            // Применение операции морфологического открытия для удаления шумов и небольших объектов
            OpenOperation();

            ConvertToBinary();

            Debug.WriteLine("Morphological Operations - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
        }

        private void CloseOperation()
        {
            // Создание структурирующего элемента для операции закрытия
            int[,] structuringElement = new int[,]
            {
        { 1, 1, 1 },
        { 1, 1, 1 },
        { 1, 1, 1 }
            };

            // Применение операции дилатации
            Dilation(structuringElement);

            // Применение операции эрозии
            Erosion(structuringElement);
        }

        private void OpenOperation()
        {
            // Создание структурирующего элемента для операции открытия
            int[,] structuringElement = new int[,]
            {
        { 1, 1, 1 },
        { 1, 1, 1 },
        { 1, 1, 1 }
            };

            // Применение операции эрозии
            Erosion(structuringElement);

            // Применение операции дилатации
            Dilation(structuringElement);
        }


        private void Erosion(int[,] structuringElement)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

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
            {
                structuringElementValues[i] = (byte)(structuringElement[i / 3, i % 3] * 255);
            }

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

                            if (!shouldErode)
                                break;
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

            processFrame = (Bitmap)result.Clone();
            result.Dispose();

            Debug.WriteLine("Erosion - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
        }


        private void Dilation(int[,] structuringElement)
{
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    // Создание нового Bitmap для результата
    Bitmap result = new Bitmap(processFrame.Width, processFrame.Height);

    // Получение данных изображения в формате BitmapData
    BitmapData srcData = processFrame.LockBits(new Rectangle(0, 0, processFrame.Width, processFrame.Height),
        ImageLockMode.ReadOnly, processFrame.PixelFormat);

    BitmapData destData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
        ImageLockMode.WriteOnly, result.PixelFormat);

    // Размеры структурирующего элемента
    int structWidth = structuringElement.GetLength(1);
    int structHeight = structuringElement.GetLength(0);

    // Получение формата пикселей
    PixelFormat pixelFormat = processFrame.PixelFormat;

    // Размеры одного пикселя в байтах
    int pixelSize = Image.GetPixelFormatSize(pixelFormat) / 8;

    // Позиция начала данных исходного изображения
    IntPtr srcScan0 = srcData.Scan0;

    // Позиция начала данных результирующего изображения
    IntPtr destScan0 = destData.Scan0;

    // Вычисление смещения для доступа к пикселям в данных изображений
    int srcStride = srcData.Stride;
    int destStride = destData.Stride;

    // Итерация по пикселям изображения
    for (int y = 1; y < processFrame.Height - 1; y++)
    {
        for (int x = 1; x < processFrame.Width - 1; x++)
        {
            bool shouldDilate = false;

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    // Получение значения пикселя структурирующего элемента
                    int structElementValue = structuringElement[i + 1, j + 1];

                    // Вычисление позиции текущего пикселя в данных изображения
                    int pixelX = x + j;
                    int pixelY = y + i;

                    // Проверка границ пикселя
                    if (pixelX >= 0 && pixelX < processFrame.Width && pixelY >= 0 && pixelY < processFrame.Height)
                    {
                        // Получение адреса текущего пикселя в данных изображения
                        IntPtr srcPixelAddress = srcScan0 + pixelY * srcStride + pixelX * pixelSize;

                        // Получение цвета пикселя
                        Color pixelColor = Color.FromArgb(Marshal.ReadInt32(srcPixelAddress));

                        // Сравнение значения пикселя структурирующего элемента с белым цветом
                        if (structElementValue == 1 && pixelColor.ToArgb() == Color.White.ToArgb())
                        {
                            shouldDilate = true;
                            break;
                        }
                    }
                }

                if (shouldDilate)
                {
                    break;
                }
            }

            // Вычисление адреса текущего пикселя в данных результирующего изображения
            IntPtr destPixelAddress = destScan0 + y * destStride + x * pixelSize;

            // Установка цвета пикселя в результирующем изображении
            if (shouldDilate)
            {
                Marshal.WriteInt32(destPixelAddress, Color.White.ToArgb());
            }
            else
            {
                Marshal.WriteInt32(destPixelAddress, Color.Black.ToArgb());
            }
        }
    }

    // Освобождение данных изображений
    processFrame.UnlockBits(srcData);
    result.UnlockBits(destData);

    // Клонирование результата и освобождение ресурсов
    processFrame = (Bitmap)result.Clone();
    result.Dispose();

    stopwatch.Stop();
    Debug.WriteLine("Dilation - " + stopwatch.ElapsedMilliseconds);
}


        private void ConvertToBinary()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int y = 0; y < processFrame.Height; y++)
            {
                for (int x = 0; x < processFrame.Width; x++)
                {
                    Color pixel = processFrame.GetPixel(x, y);

                    if (pixel.GetBrightness() > 0.6)
                        processFrame.SetPixel(x, y, Color.White);
                    else
                        processFrame.SetPixel(x, y, Color.Black);
                }
            }
            Debug.WriteLine("Binary - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
        }

        private void DrawMovingObjectContours(Bitmap diffImage)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
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
                {
                    g.DrawRectangle(redPen, rect);
                }
            }

            redPen.Dispose();
            Debug.WriteLine("Drawing - " + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
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

        private Bitmap ScaleBitmap(Bitmap sourceBitmap)
        {
            int targetWidth;
            int targetHeight;
            double scalePercentage;

            // Определение уровня сжатия в зависимости от разрешения Bitmap
            if (sourceBitmap.Width >= 1920 || sourceBitmap.Height >= 1080)
            {
                scalePercentage = 0.2; // 80% сжатие для высокого разрешения
            }
            else if (sourceBitmap.Width >= 1280 || sourceBitmap.Height >= 720)
            {
                scalePercentage = 0.3; // 70% сжатие для среднего разрешения
            }
            else
            {
                scalePercentage = 0.4; // 60% сжатие для низкого разрешения
            }
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