document.addEventListener('DOMContentLoaded', function () {
    const chatToggle = document.getElementById('ai-chat-toggle');
    const chatHeader = document.querySelector('.ai-chat-header');
    const chatBody = document.getElementById('aiChatBody');
    const aiInput = document.getElementById('aiInput');
    const aiSend = document.getElementById('aiSend');
    const aiMessages = document.getElementById('aiMessages');

    function addMessage(text, who) {
        const div = document.createElement('div');
        div.className = 'ai-message ' + (who === 'user' ? 'user' : 'assistant');
        div.textContent = text;
        aiMessages.appendChild(div);
        aiMessages.scrollTop = aiMessages.scrollHeight;
    }

    chatHeader.addEventListener('click', () => {
        chatBody.classList.toggle('d-none');
        chatToggle.textContent = chatBody.classList.contains('d-none') ? '–' : '×';
    });

    async function sendMessage() {
        const text = aiInput.value.trim();
        if (!text) return;
        addMessage(text, 'user');
        aiInput.value = '';
        addMessage('Đang gửi...', 'assistant');

        try {
            const res = await fetch('/api/ai/chat', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: text })
            });
            const json = await res.json();
            // remove 'Đang gửi...'
            const last = aiMessages.lastChild;
            if (last && last.textContent === 'Đang gửi...') last.remove();
            addMessage(json.reply || 'Xin lỗi, không có phản hồi.', 'assistant');
        } catch (e) {
            const last = aiMessages.lastChild;
            if (last && last.textContent === 'Đang gửi...') last.remove();
            addMessage('Lỗi kết nối tới AI.', 'assistant');
        }
    }

    aiSend?.addEventListener('click', sendMessage);
    aiInput?.addEventListener('keydown', function (e) { if (e.key === 'Enter') sendMessage(); });
});
