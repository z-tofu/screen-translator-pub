using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace ScreenTranslator2
{
    public class EmbeddedTranslator : IDisposable
    {
        private LLamaWeights? _weights;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private string? _currentModelPath;

        public async Task LoadModelAsync(string modelPath)
        {
            if (_currentModelPath == modelPath && _weights != null)
                return;

            DisposeCurrentModel();

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 1024,
                GpuLayerCount = 20 // Will offload to GPU if a CUDA backend is installed
            };

            _weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters));
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
            _currentModelPath = modelPath;
        }

        public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang)
        {
            if (_executor == null)
            {
                return "Error: Model not loaded.";
            }

            string prompt;
            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 256,
                AntiPrompts = new List<string> { "User:", "SOURCE TEXT:", "Text:", "<|user|>", "<|end|>", "[INST]" }
            };

            if (_currentModelPath != null && _currentModelPath.Contains("phi", StringComparison.OrdinalIgnoreCase))
            {
                prompt = $"<|user|>\nYou are a strict translation API. Translate the following text from {sourceLang} to {targetLang}. Output ONLY the translated text. Do not add any conversational filler, notes, or descriptions.\n\nText: {text}<|end|>\n<|assistant|>\n";
            }
            else if (_currentModelPath != null && _currentModelPath.Contains("mistral", StringComparison.OrdinalIgnoreCase))
            {
                prompt = $"[INST] You are a strict translation API. Translate the following text from {sourceLang} to {targetLang}. Output ONLY the translated text. Do not add any conversational filler, notes, or descriptions.\n\nText: {text} [/INST]";
            }
            else
            {
                prompt = $"You are a strict translation engine. Output EXACTLY and ONLY the translation of the text from {sourceLang} to {targetLang}. Do NOT include any conversational filler, explanations, or notes.\n\nSOURCE TEXT:\n{text}\n\nTRANSLATION:\n";
            }

            string result = "";
            await foreach (var token in _executor.InferAsync(prompt, inferenceParams))
            {
                result += token;
            }

            return result.Trim();
        }

        private void DisposeCurrentModel()
        {
            _context?.Dispose();
            _weights?.Dispose();
            _context = null;
            _weights = null;
            _executor = null;
        }

        public void Dispose()
        {
            DisposeCurrentModel();
        }
    }
}
