var gulp = require('gulp');
var del = require('del');
var print = require('gulp-print');
var babel = require('gulp-babel');

gulp.task('clean', () => del('root'));

gulp.task('js',['clean'], () => {
  return gulp.src('src/**/*.js')
    .pipe(print())
  .pipe(babel({presets: ['es2015']}))
  .pipe(gulp.dest('root'));
});

gulp.task('default', ['js'], () => {
  gulp.src('src/**/!(*.js)')
    .pipe(print())
    .pipe(gulp.dest('root'));
});
  


