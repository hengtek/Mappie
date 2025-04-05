# 命令说明 Command Instructions

> 存在跨进程访存，务必管理员权限执行命令 \
> Cross-process memory access is involved; ensure commands are executed with administrator privileges.

`list` -> 显示所有已加载地图，请在 Cordycep 加载地图

`list` -> Displays all loaded maps. Please load the map in Cordycep.

`dump 地图名（上面list显示的）` -> 导出所有地图（包括地图的cast模型，和材质名）

`dump mapname (as shown in the list above)` -> Exports all maps (including the map's cast models and material names).

`dump 地图名（上面list显示的） -onlyjson` -> 导出地图json文件，但surface仍然导出为模型（模型库并没有面的模型）同时导出surface的材质名

`dump mapname (as shown in the list above) -onlyjson` -> Exports the map's JSON file, but surfaces are still exported as models (the model library does not include surface models). Also exports the material names of the surfaces.

# 更新 Updates

- 多线程加速，surface提取更快
- Multi-threading acceleration for faster surface extraction.
- 支持导出json文件
- Support for exporting JSON files.
- 调整代码结构
- Adjusted code structure.
- NMW3已测试，NMW3 SP，BO6编写未测试
- Tested on NMW3; NMW3 SP and BO6 scripts are written but not tested.

---

# DotneskRemastered

A rewritten version of a private map extractor by me. Now using [Cordycep](https://github.com/Scobalula/Cordycep) for newer Call of Duty titles!

## Output
``*_images_list``: contains list of images needed for texturing, you'll need to use [Greyhound](https://github.com/Scobalula/Greyhound) for extraction

``*_images``: contains a list of semantic of a material needed for shading/texturing

## Libraries used
1. [Cast.NET](https://github.com/Scobalula/Cast.NET)
