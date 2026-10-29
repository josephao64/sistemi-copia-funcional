(() => {
    'use strict';
    const payload=document.getElementById('control-picker-data'); if(!payload)return;
    const data=JSON.parse(payload.textContent);
    const normalize=value=>value.normalize('NFD').replace(/\p{Diacritic}/gu,'').toLocaleLowerCase();
    document.querySelectorAll('[data-control-picker]').forEach(input=>{
        const holder=input.closest('label'),id=holder.querySelector('input[type=hidden]'),results=holder.querySelector('[data-picker-results]');
        const list=data[input.dataset.controlPicker]||[];
        input.value=list.find(x=>x.id===id.value)?.name||'';
        input.addEventListener('input',()=>{
            id.value='';results.replaceChildren();
            if(!input.value.trim())return;
            list.filter(x=>normalize(x.name).includes(normalize(input.value))).slice(0,12).forEach(item=>{
                const button=document.createElement('button');button.type='button';button.textContent=item.name;
                button.addEventListener('click',()=>{id.value=item.id;input.value=item.name;results.replaceChildren();});
                results.append(button);
            });
        });
        document.addEventListener('click',event=>{if(!holder.contains(event.target))results.replaceChildren();});
    });
})();
