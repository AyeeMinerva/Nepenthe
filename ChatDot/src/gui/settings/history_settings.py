import os
import shutil
from datetime import datetime
from PyQt5.QtWidgets import (QWidget, QVBoxLayout, QHBoxLayout, QLabel, 
                            QPushButton, QScrollArea, QFrame, 
                            QMessageBox, QSizePolicy, QFileDialog)
from PyQt5.QtCore import Qt, pyqtSignal
from core.global_managers.service_manager import ServiceManager
from core.bootstrap import Bootstrap

class HistoryFileItem(QFrame):
    """历史文件项组件，显示一个历史文件及其操作按钮"""
    delete_requested = pyqtSignal(str)  # 请求删除文件的信号
    load_requested = pyqtSignal(str)    # 请求加载文件的信号
    copy_requested = pyqtSignal(str)    # 请求复制文件的信号
    
    def __init__(self, file_path, file_name, parent=None):
        super().__init__(parent)
        self.file_path = file_path
        self.file_name = file_name
        
        self.setFrameShape(QFrame.StyledPanel)
        self.setFrameShadow(QFrame.Raised)
        self.setStyleSheet("QFrame { border: 1px solid #cccccc; border-radius: 3px; background-color: #f9f9f9; }")
        
        # 创建水平布局
        layout = QHBoxLayout(self)
        
        # 文件名标签
        self.file_label = QLabel(file_name)
        self.file_label.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Preferred)
        layout.addWidget(self.file_label)
        
        # 加载按钮
        self.load_btn = QPushButton("载入")
        self.load_btn.setStyleSheet("QPushButton { min-width: 30px; }")
        self.load_btn.clicked.connect(self.emit_load_signal)
        layout.addWidget(self.load_btn)
        
        # 复制按钮
        self.copy_btn = QPushButton("复制")
        self.copy_btn.setStyleSheet("QPushButton { min-width: 30px; }")
        self.copy_btn.clicked.connect(self.emit_copy_signal)
        layout.addWidget(self.copy_btn)
        
        # 删除按钮
        self.delete_btn = QPushButton("删除")
        self.delete_btn.setStyleSheet("QPushButton { min-width: 30px; }")
        self.delete_btn.clicked.connect(self.emit_delete_signal)
        layout.addWidget(self.delete_btn)
        
        self.setLayout(layout)
    
    def emit_delete_signal(self):
        self.delete_requested.emit(self.file_path)
    
    def emit_load_signal(self):
        self.load_requested.emit(self.file_path)
        
    def emit_copy_signal(self):
        self.copy_requested.emit(self.file_path)

class HistorySettingsPage(QWidget):
    """历史记录设置页面，显示和管理聊天历史文件"""
    load_history_requested = pyqtSignal(str)  # 请求加载历史记录的信号
    
    def __init__(self, parent=None):
        super().__init__(parent)
        # 初始化 Bootstrap
        self.bootstrap = Bootstrap()
        self.bootstrap.initialize()
        
        # 获取 ServiceManager 实例
        self.service_manager = self.bootstrap.service_manager
        
        # 获取服务
        try:
            self.chat_service = self.service_manager.get_service("chat_service")
        except KeyError:
            QMessageBox.warning(self, "错误", "聊天服务未正确初始化")
            self.chat_service = None
            
        self.history_dir = os.path.join(os.getcwd(), "ChatDot_Main", "history")
        self.init_ui()
        
    def init_ui(self):
        layout = QVBoxLayout(self)
        
        # 标题
        title_label = QLabel("聊天历史记录")
        title_label.setStyleSheet("QLabel { font-weight: bold; font-size: 14px; }")
        layout.addWidget(title_label)
        
        # 操作按钮布局
        button_layout = QHBoxLayout()
        
        # 刷新按钮
        refresh_button = QPushButton("刷新历史记录列表")
        refresh_button.clicked.connect(self.load_history_file)
        button_layout.addWidget(refresh_button)
        
        # 导出按钮
        export_button = QPushButton("导出当前历史")
        export_button.clicked.connect(self.export_current_history)
        button_layout.addWidget(export_button)
        
        # 导入按钮
        import_button = QPushButton("导入历史文件")
        import_button.clicked.connect(self.import_history)
        button_layout.addWidget(import_button)
        
        # 清空按钮
        clear_button = QPushButton("清空当前历史")
        clear_button.clicked.connect(self.clear_chat_history)
        button_layout.addWidget(clear_button)
        
        layout.addLayout(button_layout)
        
        # 滚动区域用于显示文件列表
        self.scroll_area = QScrollArea()
        self.scroll_area.setWidgetResizable(True)
        self.scroll_content = QWidget()
        self.scroll_layout = QVBoxLayout(self.scroll_content)
        self.scroll_layout.setAlignment(Qt.AlignTop)
        self.scroll_area.setWidget(self.scroll_content)
        layout.addWidget(self.scroll_area)
        
        # 加载历史文件
        self.load_history_list()
        
    def load_history_list(self):
        """加载历史记录文件列表"""
        # 清空原有列表
        while self.scroll_layout.count():
            child = self.scroll_layout.takeAt(0)
            if child.widget():
                child.widget().deleteLater()
        
        # 确保目录存在
        if not os.path.exists(self.history_dir):
            os.makedirs(self.history_dir)
        
        # 加载目录下所有json文件
        files = [f for f in os.listdir(self.history_dir) if f.endswith('.json')]
        files.sort(reverse=True)  # 最新的文件在前面
        
        if not files:
            empty_label = QLabel("没有找到历史记录文件")
            empty_label.setAlignment(Qt.AlignCenter)
            self.scroll_layout.addWidget(empty_label)
            return
            
        # 添加文件项到滚动区域
        for file_name in files:
            file_path = os.path.join(self.history_dir, file_name)
            file_item = HistoryFileItem(file_path, file_name)
            file_item.delete_requested.connect(self.delete_history_file)
            file_item.load_requested.connect(self.load_history_file)
            file_item.copy_requested.connect(self.copy_history_file)
            self.scroll_layout.addWidget(file_item)
    
    def delete_history_file(self, file_path):
        """删除历史记录文件"""
        reply = QMessageBox.question(
            self, 
            '确认删除', 
            f"确定要删除此历史记录文件吗？\n{os.path.basename(file_path)}",
            QMessageBox.Yes | QMessageBox.No, 
            QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            try:
                os.remove(file_path)
                QMessageBox.information(self, "删除成功", "历史记录文件已删除")
                self.load_history_list()  # 刷新列表
            except Exception as e:
                QMessageBox.warning(self, "删除失败", f"删除文件时出错：{str(e)}")
    
    def copy_history_file(self, file_path):
        """复制历史记录文件"""
        try:
            # 获取原文件名和路径信息
            dir_name = os.path.dirname(file_path)
            file_name = os.path.basename(file_path)
            
            # 创建新的文件名 (copy)原文件名
            new_file_name = "(copy)" + file_name
            new_file_path = os.path.join(dir_name, new_file_name)
            
            # 复制文件
            shutil.copy2(file_path, new_file_path)
            QMessageBox.information(self, "复制成功", f"已创建副本: {new_file_name}")
            
            # 刷新列表
            self.load_history_list()
            
        except Exception as e:
            QMessageBox.warning(self, "复制失败", f"复制文件时出错：{str(e)}")
    
    def load_history_file(self, file_path):
        """加载历史记录文件"""
        if not self._check_service():
            return

        try:
            # 发送加载历史记录信号
            self.load_history_requested.emit(file_path)
            # 不在这里显示成功消息，让 ChatWindow 处理结果
        except Exception as e:
            QMessageBox.warning(self, "错误", f"加载历史记录失败：{str(e)}")

    def clear_chat_history(self):
        """清空当前聊天历史"""
        if not self._check_service():
            return

        reply = QMessageBox.question(
            self,
            '确认清空',
            "确定要清空当前聊天历史吗？此操作无法撤销。",
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            try:
                self.chat_service.clear_context()
                QMessageBox.information(self, "成功", "聊天历史已清空")
            except Exception as e:
                QMessageBox.warning(self, "错误", f"清空历史失败：{str(e)}")

    def export_current_history(self):
        """导出当前聊天历史"""
        if not self._check_service():
            return

        default_name = f"chat_history_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "导出聊天历史",
            os.path.join(self.history_dir, default_name),
            "JSON 文件 (*.json)"
        )
        
        if file_path:
            try:
                self.chat_service.export_history(file_path)
                QMessageBox.information(self, "成功", f"聊天历史已导出到：\n{file_path}")
                self.load_history_list()  # 刷新列表
            except Exception as e:
                QMessageBox.warning(self, "错误", f"导出失败：{str(e)}")

    def import_history(self):
        """导入聊天历史"""
        if not self._check_service():
            return

        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "导入聊天历史",
            self.history_dir,
            "JSON 文件 (*.json)"
        )
        
        if file_path:
            try:
                self.chat_service.import_history(file_path)
                QMessageBox.information(self, "成功", "聊天历史导入成功")
            except Exception as e:
                QMessageBox.warning(self, "错误", f"导入失败：{str(e)}")

    def _check_service(self):
        """检查服务是否可用"""
        if not self.chat_service:
            QMessageBox.warning(self, "错误", "聊天服务未初始化")
            return False
        return True