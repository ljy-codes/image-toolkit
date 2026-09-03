# AI 模型来源与发布复核

复核日期：2026-09-03

## 发布结论

苏影枢可以发布主程序和安装包，但不得把 AI 模型文件打包进安装程序、源码仓库或 GitHub Release 附件。

应用不会分发模型权重。用户可主动从 rembg 项目的 GitHub Release 在线下载，也可选择通过可信渠道取得的本地 ONNX 文件。两种安装方式都必须通过固定文件大小和 SHA-256 校验，图片推理在本机执行。

## 上游项目

| 项目 | 用途 | 许可证 | 来源 |
| --- | --- | --- | --- |
| BiRefNet | 高精度通用与人像背景分割模型 | MIT | https://github.com/ZhengPeng7/BiRefNet |
| rembg | ONNX 模型托管来源和背景移除实现参考 | MIT | https://github.com/danielgatis/rembg |

当前 ONNX 权重的标准来源是 rembg 的 GitHub Release。无法访问 GitHub 时，用户可以自行通过可信渠道取得同一模型文件并在应用中本地导入。BiRefNet 上游仓库采用 MIT 许可证，但该 Release 没有随文件单独提供可独立核验的 ONNX 转换链和构建脚本，因此仍禁止把权重随产品重新分发。

## 当前下载文件

| 模型 | 下载地址 | 大小 | SHA-256 |
| --- | --- | ---: | --- |
| `birefnet-general.onnx` | https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-general-epoch_244.onnx | 972,666,916 字节 | `58F621F00F5D756097615970A88A791584600DCF7C45B18A0A6267535A1EBD3C` |
| `birefnet-portrait.onnx` | https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-portrait-epoch_150.onnx | 972,666,916 字节 | `1BA1C8FF5A7BBFADC8D8D13FB11D7BE793F91F23D9D466549E37A854F6668F99` |

## 风险控制

1. 安装包、源码仓库和 Release 附件均不包含 ONNX 权重。
2. 应用界面明确显示模型来源和上游许可证。
3. 用户主动操作后才在线下载或选择本地模型，模型可在应用内删除。
4. 在线下载和本地导入都必须通过大小和 SHA-256 校验后才能安装；文件名不作为识别依据。
5. 本地导入仅复制用户选择的源文件，先写入临时文件并校验，再替换应用模型目录中的目标文件。
6. 应用和发布方不提供第三方模型文件中转；用户应确认本地文件来源可信。
7. 若未来需要随产品分发模型，必须改用具有明确权重许可证和可核验转换过程的模型，并重新完成效果、性能与授权验收。

以上内容是工程发布范围复核，不构成法律意见。
