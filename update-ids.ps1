# 批量修改实体 ID 类型从 long 改为 string 的脚本
# 使用方法：在 E:\project\OmniMind\OmniMind.Domain\Entities 目录运行

$pattern = 'public long Id \{ get; set; \}'
$replacement = 'public string Id { get; set; } = Guid.CreateVersion7().ToString();'

$files = Get-ChildItem -Filter "*.cs" -Exclude "ITenantEntity.cs", "I*.cs"

foreach ($file in $files) {
    Write-Host "Processing: $($file.Name)"
    (Get-Content $file.FullName) -replace $pattern, $replacement | Set-Content $file.FullName
}

Write-Host "Done! Please review the changes and compile."
