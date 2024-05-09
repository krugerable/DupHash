using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

public class ImageSimilarityChecker : IDisposable
{
    private readonly PerceptualHash hasher;  // Hash algorithm for computing image hashes.
    private readonly double similarityThreshold;  // Threshold for deciding image similarity, scaled to 0-1.
    private readonly string folderPath;  // The path to the folder containing images to be compared.
    private int _progress;  // Private variable for tracking progress percentage.

    // Public property for accessing the progress percentage.
    public int Progress
    {
        get { return _progress; }
        private set
        {
            _progress = value;
            OnProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_progress));
        }
    }

    // Event for notifying subscribers about progress updates.
    public delegate void ProgressChangedHandler(object sender, ProgressChangedEventArgs e);
    public event ProgressChangedHandler OnProgressChanged;

    // Class for event data containing the progress percentage.
    public class ProgressChangedEventArgs : EventArgs
    {
        public int Progress { get; }
        public ProgressChangedEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    // Public property for accessing the result of the comparison.
    // Each element in the list contains a pair of image paths and their similarity score.
    public List<Tuple<string, string, double>> Result { get; private set; }

    // Constructor that requires both folder path and similarity threshold.
    public ImageSimilarityChecker(string folderPath, double similarityThreshold)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be null or empty.", nameof(folderPath));

        if (similarityThreshold <= 0 || similarityThreshold > 100)
            throw new ArgumentException("Similarity threshold must be between 1 and 100.", nameof(similarityThreshold));

        this.folderPath = folderPath;
        this.similarityThreshold = similarityThreshold / 100.0;  // Convert percentage to decimal.
        this.hasher = new PerceptualHash();  // Initialize the hashing algorithm.
        this.Result = new List<Tuple<string, string, double>>();  // Initialize the results list.
    }

    // Helper method to safely load a Bitmap from a file path.
    private static Bitmap LoadBitmap(string path)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            return new Bitmap(fs);
        }
    }

    // Calculates the similarity score between two image hashes, scaled to 1-100.
    private double CalculateScore(ulong hash1, ulong hash2)
    {
        double similarity = 1 - (ImageHash.CompareHashes(hash1, hash2) / 64.0);
        return similarity * 100;  // Scale score to 1-100 range.
    }

    // Checks if a file has a supported image format.
    private bool ImageFormatSupported(string filename)
    {
        string[] supportedFormats = new string[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        return Array.Exists(supportedFormats, format => format.Equals(Path.GetExtension(filename).ToLowerInvariant()));
    }

    // Initiates the comparison of images in a folder in a new thread.
    public void CompareImagesInFolderAsync()
    {
        Thread thread = new Thread(CompareImagesInFolder);
        thread.Start();
    }

    // Compares all images within the specified folder recursively and groups similar images along with their similarity scores.
    private void CompareImagesInFolder()
    {
        var allImages = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        var imagesHashes = new Dictionary<string, ulong>();
        Result.Clear();  // Clear previous results.

        // Load and hash all images.
        for (int i = 0; i < allImages.Length; i++)
        {
            string imagePath = allImages[i];
            if (ImageFormatSupported(imagePath))
            {
                using (Bitmap img = LoadBitmap(imagePath))
                {
                    imagesHashes[imagePath] = hasher.Hash(img);
                }
            }
            Progress = (i + 1) * 50 / allImages.Length;  // Update progress for loading and hashing.
        }

        // Compare all hashes and group similar images.
        foreach (var pair in imagesHashes)
        {
            foreach (var innerPair in imagesHashes)
            {
                if (pair.Key != innerPair.Key)  // Ensure different images are being compared.
                {
                    double score = CalculateScore(pair.Value, innerPair.Value);
                    if (score >= similarityThreshold * 100)
                    {
                        Result.Add(new Tuple<string, string, double>(pair.Key, innerPair.Key, score));  // Store the pair and score.
                    }
                }
            }
            Progress = 100;  // Indicate completion.
        }
    }

    // IDisposable implementation to clean up resources.
    public void Dispose()
    {
        // Any necessary resource cleanup would go here.
    }
}
