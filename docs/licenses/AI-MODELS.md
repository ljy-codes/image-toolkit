# AI 模型来源与发布复核

复核日期：2026-09-03

## 发布结论

苏影枢 `v1.1.0` 可以发布主程序和安装包，但不得把 AI 模型文件打包进安装程序、源码仓库或 GitHub Release 附件。

应用仅在用户主动点击安装模型后，从 rembg 项目的 GitHub Release 下载 ONNX 文件到当前用户的本机目录。下载完成后校验固定文件大小和 SHA-256，图片推理在本机执行。

## 上游项目

| 项目 | 用途 | 许可证 | 来源 |
| --- | --- | --- | --- |
| U-2-Net | 模型架构和上游预训练模型 | Apache License 2.0 | https://github.com/xuebinqin/U-2-Net |
| rembg | ONNX 模型托管来源和背景移除实现参考 | MIT | https://github.com/danielgatis/rembg |

rembg 的模型说明将 `u2net` 和 `u2net_human_seg` 指向 U-2-Net，并声明模型许可证包含在模型下载中。当前 GitHub Release 中的 ONNX 文件没有单独提供可独立核验的转换链、构建脚本或来源证明。rembg issue `#837` 已就此问题进行过询问，但未取得更详细的来源证明。

## 当前下载文件

| 模型 | 下载地址 | 大小 | SHA-256 |
| --- | --- | ---: | --- |
| `u2net.onnx` | https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx | 175,997,641 字节 | `8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491` |
| `u2net_human_seg.onnx` | https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net_human_seg.onnx | 175,997,641 字节 | `01EB6A29A5C4D8EDB30B56ADAD9BB3A2A0535338E480724A213E0ACFD2D1C73C` |

## 风险控制

1. 安装包、源码仓库和 Release 附件均不包含 ONNX 权重。
2. 应用界面明确显示模型来源和上游许可证。
3. 用户主动操作后才下载模型，模型可在应用内删除。
4. 下载文件必须通过大小和 SHA-256 校验后才能安装。
5. 若未来需要随产品分发模型，必须改用具有明确权重许可证和可核验转换过程的模型，并重新完成效果、性能与授权验收。

以上内容是工程发布范围复核，不构成法律意见。
