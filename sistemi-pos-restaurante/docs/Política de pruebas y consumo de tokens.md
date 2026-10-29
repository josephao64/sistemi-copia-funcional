## Política de pruebas y consumo de tokens

Durante el desarrollo de SISTEMI POS, priorizar el menor consumo posible de tokens.

Después de implementar cada cambio:

* NO ejecutar automáticamente toda la suite de pruebas del proyecto.
* NO realizar pruebas visuales, de navegación ni pruebas manuales de interfaz.
* NO recorrer nuevamente todo el proyecto si no es necesario.
* NO ejecutar `restore` repetidamente si no hubo cambios en paquetes o dependencias.
* NO realizar ciclos repetitivos de `restore`, `build` y `test`.

Realizar únicamente las validaciones técnicas mínimas necesarias sobre el cambio realizado.

Si existen pruebas automatizadas directamente relacionadas con el módulo modificado, ejecutar solamente esas pruebas.

Si es necesario compilar, hacerlo una sola vez después de completar todos los cambios solicitados en el prompt, no después de cada archivo modificado.

Las pruebas funcionales, visuales, de diseño y navegación serán realizadas manualmente por mí.

Al finalizar el trabajo, únicamente indicar:

* Qué se modificó.
* Qué archivos principales fueron modificados.
* Qué validación técnica se realizó.
* Qué debo probar manualmente.
* Si existe algún riesgo o punto que requiera especial atención.

No realizar trabajo adicional, refactorizaciones, pruebas exhaustivas ni cambios fuera del alcance solicitado.
