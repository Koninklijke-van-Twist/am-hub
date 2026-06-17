using MJOP.Calculator.Models;
using System.Text.Json;

namespace MJOP.Calculator.Services;

public class EquipmentModelService
{
    private readonly string _modelsFilePath;
    private List<EquipmentModel> _models = new();
    private int _nextId = 1;

    public EquipmentModelService()
    {
        // Store models in wwwroot for persistence
        var baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(baseDirectory);
        _modelsFilePath = Path.Combine(baseDirectory, "equipment-models.json");
        
        LoadModels();
    }

    public List<EquipmentModel> GetAllModels()
    {
        return _models.OrderBy(m => m.Brand).ThenBy(m => m.ModelName).ToList();
    }

    public EquipmentModel? GetModel(int id)
    {
        return _models.FirstOrDefault(m => m.Id == id);
    }

    public EquipmentModel? GetModelByBrandAndName(string brand, string modelName)
    {
        return _models.FirstOrDefault(m => m.Brand == brand && m.ModelName == modelName);
    }

    public void AddModel(EquipmentModel model)
    {
        model.Id = _nextId++;
        _models.Add(model);
        SaveModels();
    }

    public void UpdateModel(EquipmentModel model)
    {
        var existing = _models.FirstOrDefault(m => m.Id == model.Id);
        if (existing != null)
        {
            existing.Brand = model.Brand;
            existing.ModelName = model.ModelName;
            existing.CustomCosts = model.CustomCosts;
            SaveModels();
        }
    }

    public void DeleteModel(int id)
    {
        _models.RemoveAll(m => m.Id == id);
        SaveModels();
    }

    private void LoadModels()
    {
        try
        {
            if (File.Exists(_modelsFilePath))
            {
                var json = File.ReadAllText(_modelsFilePath);
                _models = JsonSerializer.Deserialize<List<EquipmentModel>>(json) ?? new();
                _nextId = (_models.Count > 0 ? _models.Max(m => m.Id) : 0) + 1;
            }
            
        }
        catch
        {
            _models = new();
            
        }
    }


    private void SaveModels()
    {
        try
        {
            var json = JsonSerializer.Serialize(_models, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_modelsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving models: {ex.Message}");
        }
    }
}
