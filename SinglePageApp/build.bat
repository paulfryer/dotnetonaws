call npm run buildjs
call npm run buildcss
rmdir dist /s /q
mkdir dist
cd dist
mkdir js
mkdir html
mkdir css
mkdir images
cd ..
copy css\bundle.css dist\css\bundle.css
call uglifyjs js/bundle.js -o dist/js/bundle.js
copy index.html dist\index.html
copy images\* dist\images\
call html-minifier --input-dir html --output-dir dist\html --remove-comments